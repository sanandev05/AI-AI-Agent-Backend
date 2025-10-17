using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class PptxCreateTool : ITool
{
    public string Name => "PptxCreate";
    public string Description => "Creates a PowerPoint presentation (PPTX) with title slide and content slides from provided text. Supports multi-slide presentations with bullet points.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string", description = "Main presentation title" },
            bullets = new { type = "array", items = new { type = "string" }, description = "Array of bullet points or slide content" },
            fileName = new { type = "string", description = "Output filename (optional)" }
        },
        required = new[] { "title" }
    };

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var title = args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "Presentation";
        var bullets = args.TryGetProperty("bullets", out var b) && b.ValueKind == JsonValueKind.Array 
            ? b.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() 
            : new List<string>();
        var fileName = args.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String 
            ? f.GetString()! 
            : $"presentation_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pptx";
        
        if (!fileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)) 
            fileName += ".pptx";
        
        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        // Create presentation document
        using var presDoc = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
        var presentationPart = presDoc.AddPresentationPart();
        presentationPart.Presentation = new Presentation();
        
        // Create slide master and layout (required for valid PPTX)
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideMaster = new SlideMaster(
            new CommonSlideData(new ShapeTree()),
            new ColorMap()
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
            }
        );
        slideMasterPart.SlideMaster = slideMaster;
        
        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        slideLayoutPart.SlideLayout = new SlideLayout(
            new CommonSlideData(new ShapeTree()),
            new ColorMapOverride(new A.MasterColorMapping())
        );
        
        // Initialize presentation structure
        var slideIdList = new SlideIdList();
        var slideSize = new SlideSize() { Cx = 9144000, Cy = 6858000, Type = SlideSizeValues.Screen4x3 };
        var notesSize = new NotesSize() { Cx = 6858000, Cy = 9144000 };
        
        presentationPart.Presentation.SlideIdList = slideIdList;
        presentationPart.Presentation.SlideSize = slideSize;
        presentationPart.Presentation.NotesSize = notesSize;

        void AddSlide(string? slideTitle, IEnumerable<string>? slideBullets)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));
            slidePart.AddPart(slideLayoutPart);

            var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;
            
            // Non-visual group shape properties (required)
            var nonVisualGroupShapeProperties = new NonVisualGroupShapeProperties(
                new NonVisualDrawingProperties() { Id = 1U, Name = "" },
                new NonVisualGroupShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties());
            
            var groupShapeProperties = new GroupShapeProperties(
                new A.TransformGroup());
            
            shapeTree.Append(nonVisualGroupShapeProperties);
            shapeTree.Append(groupShapeProperties);

            // Title shape with proper positioning
            var titleShape = new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties() { Id = 2U, Name = "Title" },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape() { Type = PlaceholderValues.Title })
                ),
                new ShapeProperties(
                    new A.Transform2D(
                        new A.Offset() { X = 457200L, Y = 274638L },
                        new A.Extents() { Cx = 8229600L, Cy = 1143000L }
                    )
                ),
                new TextBody(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties() { Language = "en-US", FontSize = 4400 },
                            new A.Text(slideTitle ?? string.Empty)
                        ),
                        new A.EndParagraphRunProperties() { Language = "en-US" }
                    )
                )
            );
            shapeTree!.Append(titleShape);

            // Content shape with bullets (if provided)
            if (slideBullets is not null && slideBullets.Any())
            {
                var bodyProperties = new A.BodyProperties();
                var listStyle = new A.ListStyle();
                
                var contentTextBody = new TextBody(bodyProperties, listStyle);
                
                foreach (var bulletText in slideBullets)
                {
                    var paragraph = new A.Paragraph(
                        new A.ParagraphProperties(
                            new A.BulletFont() { Typeface = "Arial" },
                            new A.CharacterBullet() { Char = "•" }
                        ),
                        new A.Run(
                            new A.RunProperties() { Language = "en-US", FontSize = 2400 },
                            new A.Text(bulletText)
                        ),
                        new A.EndParagraphRunProperties() { Language = "en-US" }
                    );
                    contentTextBody.Append(paragraph);
                }
                
                var contentShape = new Shape(
                    new NonVisualShapeProperties(
                        new NonVisualDrawingProperties() { Id = 3U, Name = "Content" },
                        new NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                        new ApplicationNonVisualDrawingProperties(new PlaceholderShape() { Type = PlaceholderValues.Body, Index = 1U })
                    ),
                    new ShapeProperties(
                        new A.Transform2D(
                            new A.Offset() { X = 457200L, Y = 1600200L },
                            new A.Extents() { Cx = 8229600L, Cy = 4525963L }
                        )
                    ),
                    contentTextBody
                );
                shapeTree.Append(contentShape);
            }

            // Add slide to presentation
            var slideId = new SlideId()
            {
                Id = (UInt32Value)(slideIdList!.ChildElements.Count + 256U),
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            };
            slideIdList.Append(slideId);
        }

        // Add title slide
        AddSlide(title, null);
        
        // Add content slides with bullets (group into slides of 5-7 bullets each)
        if (bullets.Any())
        {
            const int bulletsPerSlide = 6;
            for (int i = 0; i < bullets.Count; i += bulletsPerSlide)
            {
                var slideContent = bullets.Skip(i).Take(bulletsPerSlide).ToList();
                var slideTitle = i == 0 ? "Key Points" : $"Key Points ({i / bulletsPerSlide + 1})";
                AddSlide(slideTitle, slideContent);
            }
        }

        presentationPart.Presentation.Save();

        return Task.FromResult<object>(new 
        { 
            message = "PowerPoint presentation created successfully", 
            fileName, 
            path, 
            downloadUrl = $"/api/files/{fileName}", 
            sizeBytes = new FileInfo(path).Length,
            slides = 1 + (bullets.Any() ? (int)Math.Ceiling(bullets.Count / 6.0) : 0)
        });
    }
}
