using System.Runtime.InteropServices;

namespace Orange.Core;

/// <summary>
/// Document, spreadsheet and presentation conversions through the installed Microsoft Office
/// (late-bound COM, so no Office SDK is needed). Office automation must run on an STA thread
/// and is not happy doing two things at once, so jobs are serialized.
/// </summary>
public static class Office
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static bool HasWord => Type.GetTypeFromProgID("Word.Application") != null;
    public static bool HasExcel => Type.GetTypeFromProgID("Excel.Application") != null;
    public static bool HasPowerPoint => Type.GetTypeFromProgID("PowerPoint.Application") != null;

    // ── Word ──────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> WordFormats = new()
    {
        ["pdf"] = 17, ["docx"] = 16, ["doc"] = 0, ["odt"] = 23, ["rtf"] = 6,
        ["txt"] = 2, ["html"] = 10, ["xps"] = 18,
    };

    public static IEnumerable<string> WordTargets => WordFormats.Keys;

    public static Task<string> WordConvertAsync(string input, string ext) => RunSta(() =>
    {
        var output = Paths.Output(input, ext);
        dynamic word = Create("Word.Application");
        dynamic? doc = null;
        try
        {
            word.Visible = false;
            word.DisplayAlerts = 0; // wdAlertsNone
            doc = word.Documents.Open(FileName: input, ConfirmConversions: false, ReadOnly: true,
                                      AddToRecentFiles: false, Visible: false);
            if (ext == "txt")
                doc.SaveAs2(FileName: output, FileFormat: WordFormats[ext], Encoding: 65001); // UTF-8
            else
                doc.SaveAs2(FileName: output, FileFormat: WordFormats[ext]);
            return output;
        }
        finally
        {
            if (doc != null) { doc.Close(0); Release(doc); }
            if (word.Documents.Count == 0) word.Quit(0);
            Release(word);
        }
    });

    // ── Excel ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> ExcelFormats = new()
    {
        ["pdf"] = -1, ["xlsx"] = 51, ["xls"] = 56, ["ods"] = 60, ["csv"] = 62, ["tsv"] = 42, ["html"] = 44,
    };

    public static IEnumerable<string> ExcelTargets => ExcelFormats.Keys;

    public static Task<string> ExcelConvertAsync(string input, string ext) => RunSta(() =>
    {
        var output = Paths.Output(input, ext);
        dynamic excel = Create("Excel.Application");
        dynamic? wb = null;
        try
        {
            excel.Visible = false;
            excel.DisplayAlerts = false;
            if (FileKinds.Ext(input) == "tsv")
            {
                excel.Workbooks.OpenText(Filename: input, DataType: 1, Tab: true); // xlDelimited
                wb = excel.ActiveWorkbook;
            }
            else
            {
                wb = excel.Workbooks.Open(Filename: input, UpdateLinks: 0, ReadOnly: true, AddToMru: false);
            }

            if (ext == "pdf")
                wb.ExportAsFixedFormat(Type: 0, Filename: output); // whole workbook
            else
                wb.SaveAs(Filename: output, FileFormat: ExcelFormats[ext]);
            return output;
        }
        finally
        {
            if (wb != null) { wb.Close(false); Release(wb); }
            if (excel.Workbooks.Count == 0) excel.Quit();
            Release(excel);
        }
    });

    // ── PowerPoint ────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> SlideFormats = new()
    {
        ["pdf"] = 32, ["pptx"] = 24, ["ppt"] = 1, ["odp"] = 35,
    };

    public static IEnumerable<string> PowerPointTargets => SlideFormats.Keys;

    public static Task<string> PowerPointConvertAsync(string input, string ext) => RunSta(() =>
    {
        bool images = ext is "png" or "jpg";
        var output = images ? Paths.Output(input, "", $"{ext} slides") : Paths.Output(input, ext);

        dynamic ppt = Create("PowerPoint.Application");
        dynamic? pres = null;
        try
        {
            ppt.DisplayAlerts = 1; // ppAlertsNone
            pres = ppt.Presentations.Open(FileName: input, ReadOnly: -1, Untitled: 0, WithWindow: 0);
            if (images)
            {
                // One image per slide in a new folder, 1920 px wide.
                Directory.CreateDirectory(output);
                double w = pres.PageSetup.SlideWidth, h = pres.PageSetup.SlideHeight;
                int count = pres.Slides.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic slide = pres.Slides[i];
                    slide.Export(Path.Combine(output, $"Slide {i:00}.{ext}"), ext.ToUpperInvariant(), 1920, (int)(1920 * h / w));
                    Release(slide);
                }
            }
            else
            {
                pres.SaveAs(FileName: output, FileFormat: SlideFormats[ext]);
            }
            return output;
        }
        finally
        {
            if (pres != null) { pres.Close(); Release(pres); }
            if (ppt.Presentations.Count == 0) ppt.Quit(); // don't close the user's open decks
            Release(ppt);
        }
    });

    // ── Plumbing ──────────────────────────────────────────────────────────

    private static dynamic Create(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
                   ?? throw new InvalidOperationException($"{progId.Split('.')[0]} is not installed.");
        return Activator.CreateInstance(type)!;
    }

    private static void Release(object? com)
    {
        if (com != null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
    }

    private static async Task<T> RunSta<T>(Func<T> work)
    {
        await Gate.WaitAsync();
        try
        {
            var tcs = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
                    ? new InvalidOperationException("Office could not convert this file: " + ex.Message, ex) : ex); }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return await tcs.Task;
        }
        finally
        {
            Gate.Release();
        }
    }
}
