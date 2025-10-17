using System.Text.Json;
using System.IO;
using AI.Agent.Domain.Events;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
// Avoid bringing the entire Drawing namespace into scope to prevent name collisions
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace AI.Agent.Application.Tools;

public sealed class DocxCreateTool : ITool
{
    public string Name => "Docx.Create";

    public Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var title = input.TryGetProperty("title", out var t) ? t.GetString() ?? "Report" : "Report";
        var fromStep = input.TryGetProperty("bodyFromStep", out var bs) ? bs.GetString() : null;
        var body = fromStep is not null && ctx.TryGetValue($"step:{fromStep}:payload", out var p) ? p?.ToString() ?? string.Empty : string.Empty;
        // Optional: embed images from previous steps
        var imagesFromSteps = new List<string>();
        if (input.TryGetProperty("imagesFromSteps", out var ifs) && ifs.ValueKind == JsonValueKind.Array)
        {
            foreach (var idEl in ifs.EnumerateArray())
            {
                if (idEl.ValueKind == JsonValueKind.String)
                {
                    var sid = idEl.GetString(); if (!string.IsNullOrWhiteSpace(sid)) imagesFromSteps.Add(sid!);
                }
            }
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        using (var doc = WordprocessingDocument.Create(tmp, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var bodyEl = mainPart.Document.Body!;
            bodyEl.Append(new Paragraph(new Run(new Text(title))) { ParagraphProperties = new ParagraphProperties(new Justification() { Val = JustificationValues.Center }) });
            bodyEl.Append(new Paragraph(new Run(new Text(body ?? string.Empty))));

            // Add images below body
            foreach (var sid in imagesFromSteps)
            {
                if (!ctx.TryGetValue($"step:{sid}:artifacts", out var artsObj)) continue;
                // supports either IEnumerable<Artifact> or string[] of paths
                IEnumerable<(string path,string mime,string name)> materialize()
                {
                    if (artsObj is IEnumerable<object> any)
                    {
                        foreach (var aobj in any)
                        {
                            string? path=null, mime=null, name=null;
                            try
                            {
                                var json = System.Text.Json.JsonSerializer.Serialize(aobj);
                                using var jd = System.Text.Json.JsonDocument.Parse(json);
                                var root = jd.RootElement;
                                path = root.TryGetProperty("Path", out var pe) ? pe.GetString() : (root.TryGetProperty("path", out var pl) ? pl.GetString() : null);
                                mime = root.TryGetProperty("MimeType", out var me) ? me.GetString() : (root.TryGetProperty("mimeType", out var ml) ? ml.GetString() : null);
                                name = root.TryGetProperty("FileName", out var fe) ? fe.GetString() : (path is not null ? System.IO.Path.GetFileName(path) : null);
                            }
                            catch { }
                            if (!string.IsNullOrWhiteSpace(path)) yield return (path!, mime ?? "", name ?? System.IO.Path.GetFileName(path!));
                        }
                    }
                    else if (artsObj is IEnumerable<string> paths)
                    {
                        foreach (var pth in paths) if (!string.IsNullOrWhiteSpace(pth)) yield return (pth, "", System.IO.Path.GetFileName(pth));
                    }
                }

                foreach (var (path, mime, name) in materialize())
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                        var ext = System.IO.Path.GetExtension(path);
                        var mimeLocal = mime; // avoid assigning to foreach iteration variable
                        if (string.IsNullOrWhiteSpace(mimeLocal))
                        {
                            mimeLocal = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                                : (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) ? "image/jpeg"
                                : (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif" : "");
                        }
                        if (string.IsNullOrWhiteSpace(mimeLocal) || !mimeLocal.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;

                        // OpenXML SDK >= 3.0 uses PartTypeInfo via the static ImagePartType helpers
                        PartTypeInfo ipt = mimeLocal!.EndsWith("png", StringComparison.OrdinalIgnoreCase) ? ImagePartType.Png
                            : (mimeLocal.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || mimeLocal.Contains("jpg", StringComparison.OrdinalIgnoreCase)) ? ImagePartType.Jpeg
                            : ImagePartType.Png;

                        var imagePart = mainPart.AddImagePart(ipt);
                        using (var fs = File.OpenRead(path))
                        { imagePart.FeedData(fs); }
                        var relId = mainPart.GetIdOfPart(imagePart);

                        // Create drawing element
                        var drawing = new Drawing(
                            new DW.Inline(
                                new DW.Extent() { Cx = 6000000, Cy = 3380000 }, // approx 6in x 3.38in
                                new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                                new DW.DocProperties() { Id = (DocumentFormat.OpenXml.UInt32Value)1U, Name = "Picture" },
                                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                                new A.Graphic(new A.GraphicData(
                                    new A.Picture(
                                        new A.NonVisualPictureProperties(
                                            new A.NonVisualDrawingProperties() { Id = (DocumentFormat.OpenXml.UInt32Value)0U, Name = name },
                                            new A.NonVisualPictureDrawingProperties()
                                        ),
                                        new A.BlipFill(
                                            new A.Blip() { Embed = relId },
                                            new A.Stretch(new A.FillRectangle())
                                        ),
                                        new A.ShapeProperties(
                                            new A.Transform2D(
                                                new A.Offset() { X = 0L, Y = 0L },
                                                new A.Extents() { Cx = 6000000, Cy = 3380000 }
                                            ),
                                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                                        )
                                    )
                                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                            ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
                        );

                        bodyEl.Append(new Paragraph(new Run(drawing)));
                    }
                    catch { /* ignore bad artifacts */ }
                }
            }
            mainPart.Document.Save();
        }

        var fi = new FileInfo(tmp);
        var artifact = new Artifact(fi.Name, fi.FullName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fi.Length);
        return Task.FromResult<(object? payload, IList<Artifact> artifacts, string summary)>((new { title }, new List<Artifact> { artifact }, $"DOCX created: {fi.Name}"));
    }
}
