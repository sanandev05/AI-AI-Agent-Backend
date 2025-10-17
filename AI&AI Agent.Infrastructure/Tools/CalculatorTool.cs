using System;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class CalculatorTool : ITool
{
    public string Name => "Calculator";
    public string Description => "Safely evaluate arithmetic expressions (e.g., '97.69e9 / 12') and log the calculation clearly.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new 
        { 
            expression = new { type = "string", description = "Math expression to evaluate" },
            unit = new { type = "string", description = "Optional unit for the result (e.g., 'USD', 'months')" }
        },
        required = new[] { "expression" }
    };

    private readonly ILogger<CalculatorTool> _logger;

    public CalculatorTool(ILogger<CalculatorTool> logger)
    {
        _logger = logger;
    }

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var expr = args.GetProperty("expression").GetString() ?? string.Empty;
        var unit = args.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
        
        if (string.IsNullOrWhiteSpace(expr))
            return Task.FromResult<object>(new { error = "Expression is required", success = false });

        try
        {
            _logger.LogInformation("Executing calculation: {Expression}", expr);
            
            // Use DataTable.Compute for safe, simple arithmetic
            var dt = new DataTable();
            var result = dt.Compute(expr, null);
            
            string formattedResult;
            if (result is IFormattable f)
            {
                formattedResult = f.ToString(null, CultureInfo.InvariantCulture);
            }
            else
            {
                formattedResult = result?.ToString() ?? "";
            }
            
            var resultWithUnit = string.IsNullOrEmpty(unit) ? formattedResult : $"{formattedResult} {unit}";
            
            _logger.LogInformation("Calculation result: {Expression} = {Result}", expr, resultWithUnit);
            
            return Task.FromResult<object>(new { 
                success = true,
                expression = expr, 
                result = formattedResult,
                resultWithUnit = resultWithUnit,
                unit = unit,
                message = $"Calculated: {expr} = {resultWithUnit}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calculation failed for expression: {Expression}", expr);
            return Task.FromResult<object>(new { 
                success = false,
                expression = expr, 
                error = ex.Message,
                message = "Calculation failed"
            });
        }
    }
}
