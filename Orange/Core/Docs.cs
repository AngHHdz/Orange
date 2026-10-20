using System.Diagnostics;
using System.Text;

namespace Orange.Core;

/// <summary>
/// Text documents. Word handles the Office formats (best fidelity); Pandoc handles
/// Markdown and EPUB. PDF from Markdown/EPUB goes Pandoc → DOCX → Word → PDF.
/// </summary>
public static class Docs
{
    private static readonly string[] PandocFormats = ["md", "markdown", "epub"];

    public static string[] TargetsFor(string input) => FileKinds.Ext(input) switch
    {
        "md" or "markdown" => ["pdf", "docx", "html", "epub", "odt", "txt"],
        "epub" => ["pdf", "docx", "md", "html", "txt"],
        _ => ["pdf", "docx", "odt", "rtf", "txt", "html", "md", "epub", "doc"],
    };

    public static async Task<string> ConvertAsync(string input, string ext)
    {
        var src = FileKinds.Ext(input);
        bool pandoc = PandocFormats.Contains(src) || PandocFormats.Contains(ext);
        if (!pandoc) return await Office.WordConvertAsync(input, ext);

        if (ext == "pdf")
        {
            // Pandoc's own PDF output needs LaTeX; going through Word looks better anyway.
            var tempDocx = Path.Combine(Path.GetTempPath(), $"orange-{Guid.NewGuid():N}.docx");
            try
            {
                await RunPandocAsync(input, tempDocx, "");
                var pdf = await Office.WordConvertAsync(tempDocx, "pdf");
                var output = Paths.Output(input, "pdf");
                File.Move(pdf, output);
                return output;
            }
            finally
            {
                if (File.Exists(tempDocx)) File.Delete(tempDocx);
            }
        }

        var target = Paths.Output(input, ext);
        string args = ext switch
        {
            "md" => "-t gfm --wrap=none",
            "txt" => "-t plain --wrap=none",
            "html" => "-s",
            "epub" => $"-M title=\"{Path.GetFileNameWithoutExtension(input)}\"",
            _ => "",
        };
        await RunPandocAsync(input, target, args);
        return target;
    }

    private static async Task RunPandocAsync(string input, string output, string args)
    {
        var exe = Tools.Pandoc ?? throw new InvalidOperationException(
            "Pandoc was not found. Install it with: winget install JohnMacFarlane.Pandoc");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"\"{input}\" -o \"{output}\" {args}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var proc = Process.Start(psi)!;
        var err = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(err) ? $"pandoc exited with code {proc.ExitCode}" : err.Trim());
    }
}
