using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Tools;

public sealed class ChartCreateTool : ITool
{
    public string Name => "Chart.Create";

    [SupportedOSPlatform("windows")]
    public Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var title = input.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? (t.GetString() ?? "Chart") : "Chart";
        var labels = new List<string>();
        var values = new List<double>();
        if (input.TryGetProperty("labels", out var ls) && ls.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var el in ls.EnumerateArray()) if (el.ValueKind == System.Text.Json.JsonValueKind.String) labels.Add(el.GetString() ?? "");
        }
        if (input.TryGetProperty("values", out var vs) && vs.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var el in vs.EnumerateArray())
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.Number) values.Add(el.GetDouble());
                else if (el.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(el.GetString(), out var d)) values.Add(d);
            }
        }
        if (labels.Count == 0 || values.Count == 0 || labels.Count != values.Count)
            throw new ArgumentException("labels and values arrays must be non-empty and of equal length");

        var width = 800; var height = 480; var margin = 60; var barGap = 12;
        var imgPath = Path.Combine(Path.GetTempPath(), $"chart_{Guid.NewGuid():N}.png");

        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var axisPen = new Pen(Color.Black, 2);
        using var barBrush = new SolidBrush(Color.SteelBlue);
        using var titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 10);

        // Axes
        var chartLeft = margin; var chartBottom = height - margin; var chartRight = width - margin; var chartTop = margin;
        g.DrawLine(axisPen, chartLeft, chartBottom, chartRight, chartBottom);
        g.DrawLine(axisPen, chartLeft, chartBottom, chartLeft, chartTop);

        // Scale
        var maxVal = Math.Max(1e-9, values.Max());
        var n = values.Count;
        var slotWidth = (chartRight - chartLeft) / (double)n;
        var barWidth = Math.Max(4, (int)(slotWidth - barGap));

        for (int i = 0; i < n; i++)
        {
            var val = values[i];
            var barHeight = (int)((val / maxVal) * (chartBottom - chartTop));
            var x = (int)(chartLeft + i * slotWidth + barGap / 2.0);
            var y = chartBottom - barHeight;
            g.FillRectangle(barBrush, x, y, barWidth, barHeight);
            // Label
            var lbl = labels[i];
            var sz = g.MeasureString(lbl, labelFont);
            g.DrawString(lbl, labelFont, Brushes.Black, x + Math.Max(0, (barWidth - sz.Width) / 2), chartBottom + 4);
        }

        // Title
        var titleSize = g.MeasureString(title, titleFont);
        g.DrawString(title, titleFont, Brushes.Black, (width - titleSize.Width) / 2, 8);

        bmp.Save(imgPath, ImageFormat.Png);

        var fi = new FileInfo(imgPath);
        var artifact = new Artifact(fi.Name, fi.FullName, "image/png", fi.Length);
        var payload = new { title, labels, values, image = fi.Name };
        return Task.FromResult(((object?)payload, (IList<Artifact>)new List<Artifact> { artifact }, $"Chart created: {fi.Name}"));
    }
}
