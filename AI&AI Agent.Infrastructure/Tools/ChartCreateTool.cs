using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using SkiaSharp;

namespace AI_AI_Agent.Infrastructure.Tools;

public class ChartCreateTool : ITool
{
    public string Name => "ChartCreate";
    public string Description => "Creates a simple bar or line chart PNG from numeric series and labels.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string", description = "Chart title." },
            kind = new { type = "string", description = "Chart kind: 'bar' or 'line' (default 'bar')." },
            labels = new { type = "array", items = new { type = "string" }, description = "Labels for the x-axis." },
            values = new { type = "array", items = new { type = "number" }, description = "Numeric values for the y-axis." },
            fileName = new { type = "string", description = "Output file name (must end with .png)." },
            width = new { type = "number", description = "Image width (default 800)." },
            height = new { type = "number", description = "Image height (default 450)." }
        },
        required = new[] { "labels", "values" }
    };

    private readonly string _workspace;
    public ChartCreateTool()
    {
        _workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var labels = args.GetProperty("labels").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
        var values = args.GetProperty("values").EnumerateArray().Select(x => x.GetDouble()).ToList();
        if (labels.Count != values.Count || labels.Count == 0)
        {
            return Task.FromResult<object>("Error: 'labels' and 'values' must have equal non-zero length.");
        }

        var title = args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "Chart";
        var kind = args.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? (k.GetString() ?? "bar") : "bar";
        var width = args.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 800;
        var height = args.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 450;
        var fileName = args.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString()! : $"chart_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.png";
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) fileName += ".png";
        var path = Path.Combine(_workspace, fileName);

        // Create SkiaSharp surface
        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Margins and plotting area
        int left = 60, right = 20, top = 40, bottom = 60;
        int plotLeft = left, plotRight = width - right, plotTop = top, plotBottom = height - bottom;
        int plotWidth = plotRight - plotLeft, plotHeight = plotBottom - plotTop;

        // Title
        using (var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default,
            TextSize = 24
        })
        {
            float titleWidth = titlePaint.MeasureText(title);
            canvas.DrawText(title, (width - titleWidth) / 2, 24, titlePaint);
        }

        // Axes
        using (var axisPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, StrokeWidth = 1 })
        {
            canvas.DrawLine(plotLeft, plotBottom, plotRight, plotBottom, axisPaint);
            canvas.DrawLine(plotLeft, plotTop, plotLeft, plotBottom, axisPaint);
        }

        // Scale
        double maxVal = Math.Max(1e-9, values.Max());
        int n = values.Count;

        if (kind.Equals("line", StringComparison.OrdinalIgnoreCase))
        {
            using var linePaint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = true, StrokeWidth = 2 };
            SKPoint? prev = null;
            for (int i = 0; i < n; i++)
            {
                float x = plotLeft + (float)((i + 0.5) / n * plotWidth);
                float y = plotBottom - (float)(values[i] / maxVal * plotHeight);
                var pt = new SKPoint(x, y);
                if (prev.HasValue)
                {
                    canvas.DrawLine(prev.Value, pt, linePaint);
                }
                prev = pt;
            }
        }
        else
        {
            int barGap = 8;
            float barWidth = (float)plotWidth / n - barGap;
            using var barPaint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = true, Style = SKPaintStyle.Fill };
            for (int i = 0; i < n; i++)
            {
                float x = plotLeft + i * (barWidth + barGap);
                float hval = (float)(values[i] / maxVal * plotHeight);
                var rect = new SKRect(x, plotBottom - hval, x + barWidth, plotBottom);
                canvas.DrawRect(rect, barPaint);
            }
        }

        // X labels
        using (var labelPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default,
            TextSize = 14
        })
        {
            for (int i = 0; i < n; i++)
            {
                float x = kind.Equals("line", StringComparison.OrdinalIgnoreCase)
                    ? plotLeft + (float)((i + 0.5) / n * plotWidth)
                    : plotLeft + i * (plotWidth / (float)n) + 4;
                var label = labels[i];
                float labelWidth = labelPaint.MeasureText(label);
                canvas.DrawText(label, x - labelWidth / 2, plotBottom + 20, labelPaint);
            }
        }

        // Save PNG
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var fs = File.Open(path, FileMode.Create, FileAccess.Write))
        {
            data.SaveTo(fs);
        }

        return Task.FromResult<object>(new { message = "Chart created", fileName, path, downloadUrl = $"/api/files/{fileName}", sizeBytes = new FileInfo(path).Length });
    }
}
