using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AI_AI_Agent.Application.Tools;
using System.Text.Json;

namespace AI_AI_Agent.Tests
{
    /// <summary>
    /// Simple manual test class to verify web research functionality.
    /// This can be used to test the WebContentFetchTool, WebResearchAgentTool, and SummarizeTool integration.
    /// </summary>
    public class WebResearchTestHelper
    {
        public static async Task<string> TestWebContentFetch(string url)
        {
            var httpClientFactory = new MockHttpClientFactory();
            var logger = new MockLogger<WebContentFetchTool>();
            
            var tool = new WebContentFetchTool(httpClientFactory, logger);
            var input = JsonSerializer.SerializeToElement(new { url = url });
            var ctx = new Dictionary<string, object?>();
            
            try
            {
                var result = await tool.RunAsync(input, ctx, CancellationToken.None);
                return $"SUCCESS: {result.summary}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }
        
        public static async Task<string> TestSummarization(string text)
        {
            var tool = new SummarizeTool();
            var input = JsonSerializer.SerializeToElement(new 
            { 
                text = text, 
                mode = "smart" 
            });
            var ctx = new Dictionary<string, object?>();
            
            try
            {
                var result = await tool.RunAsync(input, ctx, CancellationToken.None);
                return $"Summary: {result.payload}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }
    }
    
    // Mock implementations for testing
    public class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
    
    public class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}