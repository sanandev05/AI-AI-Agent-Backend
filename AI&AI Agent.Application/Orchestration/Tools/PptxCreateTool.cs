using System.Text;
using AI_AI_Agent.Domain.Events;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace AI_AI_Agent.Application.Tools;

public sealed class PptxCreateTool : ITool
{
    public string Name => "Pptx.Create";

    public Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var title = input.TryGetProperty("title", out var t) ? (t.GetString() ?? "Presentation") : "Presentation";
        var fromStep = input.TryGetProperty("fromStep", out var fs) ? fs.GetString() : null;
        var bullets = new List<string>();

        // Prefer structured text from a previous step (e.g., Summarize output)
        if (!string.IsNullOrWhiteSpace(fromStep) && ctx.TryGetValue($"step:{fromStep}:payload", out var payload) && payload is not null)
        {
            var text = payload is string s ? s : System.Text.Json.JsonSerializer.Serialize(payload);
            // Split into bullet candidates
            bullets = text.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.Trim(' ', '-', '•', '\t', '\r'))
                          .Where(l => l.Length > 0)
                          .Take(20)
                          .ToList();
        }

        // Create PPTX in temp and return as artifact
        var tmp = Path.Combine(Path.GetTempPath(), $"slides_{Guid.NewGuid():N}.pptx");
        using (var presDoc = PresentationDocument.Create(tmp, PresentationDocumentType.Presentation))
        {
            var presentationPart = presDoc.AddPresentationPart();
            presentationPart.Presentation = new Presentation();
            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            slideMasterPart.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));
            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));
            var slideIdList = new SlideIdList();
            presentationPart.Presentation.Append(slideIdList);
            presentationPart.Presentation.Append(new SlideSize() { Cx = 9144000, Cy = 6858000 });

            void AddSlide(string? slideTitle, IEnumerable<string>? lines)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));
                var shapeTree = slidePart.Slide.CommonSlideData.ShapeTree;

                // Title
                var titleShape = new Shape(
                    new NonVisualShapeProperties(new NonVisualDrawingProperties() { Id = (UInt32Value)1U, Name = "Title" }, new NonVisualShapeDrawingProperties(), new ApplicationNonVisualDrawingProperties()),
                    new ShapeProperties(),
                    new TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(slideTitle ?? string.Empty))))
                );
                shapeTree.Append(titleShape);

                if (lines is not null)
                {
                    var body = new TextBody(new A.BodyProperties(), new A.ListStyle());
                    foreach (var line in lines)
                    {
                        body.Append(new A.Paragraph(new A.Run(new A.Text(line))));
                    }
                    var bulletsShape = new Shape(
                        new NonVisualShapeProperties(new NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Content" }, new NonVisualShapeDrawingProperties(), new ApplicationNonVisualDrawingProperties()),
                        new ShapeProperties(), body);
                    shapeTree.Append(bulletsShape);
                }

                var id = (UInt32Value)(slideIdList.ChildElements.Count + 256U);
                slideIdList.Append(new SlideId() { Id = id, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            }

            AddSlide(title, null);
            if (bullets.Count > 0) AddSlide("Main Points", bullets);

            presentationPart.Presentation.Save();
        }

        var fi = new FileInfo(tmp);
        var art = new Artifact(fi.Name, fi.FullName, "application/vnd.openxmlformats-officedocument.presentationml.presentation", fi.Length);
        var payloadOut = new { title, bullets = bullets.Count, file = fi.Name };
        return Task.FromResult(((object?)payloadOut, (IList<Artifact>)new List<Artifact> { art }, $"PPTX created: {fi.Name}"));
    }
}
