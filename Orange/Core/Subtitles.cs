using System.Text;
using System.Text.RegularExpressions;

namespace Orange.Core;

/// <summary>SRT ⇄ VTT, and either one to plain text (just the spoken lines).</summary>
public static partial class Subtitles
{
    private record Cue(string Start, string End, List<string> Lines);

    // "00:01:02,500 --> 00:01:04.000 align:start" — hours optional in VTT, either separator.
    [GeneratedRegex(@"^\s*((?:\d+:)?\d{1,2}:\d{2}[.,]\d{3})\s*-->\s*((?:\d+:)?\d{1,2}:\d{2}[.,]\d{3})")]
    private static partial Regex TimeLine();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    public static Task<string> ConvertAsync(string input, string ext) => Task.Run(() =>
    {
        var cues = Parse(File.ReadAllText(input));
        if (cues.Count == 0) throw new InvalidOperationException("No subtitles found in this file.");

        var output = Paths.Output(input, ext);
        File.WriteAllText(output, ext switch
        {
            "srt" => ToSrt(cues),
            "vtt" => ToVtt(cues),
            _ => ToText(cues),
        }, new UTF8Encoding(false));
        return output;
    });

    private static List<Cue> Parse(string text)
    {
        var cues = new List<Cue>();
        Cue? current = null;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var m = TimeLine().Match(line);
            if (m.Success)
            {
                current = new Cue(Normalize(m.Groups[1].Value), Normalize(m.Groups[2].Value), []);
                cues.Add(current);
            }
            else if (line.Length == 0)
            {
                current = null; // a blank line ends the cue
            }
            else if (current != null)
            {
                current.Lines.Add(line);
            }
            // Lines outside a cue (index numbers, WEBVTT header, NOTE blocks) are skipped.
        }
        return cues;
    }

    /// <summary>Always "HH:MM:SS.mmm".</summary>
    private static string Normalize(string t)
    {
        t = t.Replace(',', '.');
        return t.Count(c => c == ':') == 1 ? "00:" + t : t.PadLeft(12, '0');
    }

    private static string ToSrt(List<Cue> cues)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cues.Count; i++)
        {
            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{cues[i].Start.Replace('.', ',')} --> {cues[i].End.Replace('.', ',')}");
            foreach (var l in cues[i].Lines) sb.AppendLine(l);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string ToVtt(List<Cue> cues)
    {
        var sb = new StringBuilder("WEBVTT\n\n");
        foreach (var c in cues)
        {
            sb.Append($"{c.Start} --> {c.End}\n");
            foreach (var l in c.Lines) sb.Append(l).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string ToText(List<Cue> cues) =>
        string.Join(Environment.NewLine, cues.SelectMany(c => c.Lines).Select(l => Tags().Replace(l, "").Trim()).Where(l => l.Length > 0));
}
