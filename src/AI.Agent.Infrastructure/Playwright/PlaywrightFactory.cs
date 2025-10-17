using Microsoft.Playwright;

namespace AI.Agent.Infrastructure.Playwright;

public static class PlaywrightFactory
{
    public static async Task<IBrowser> CreateAsync()
    {
        var pw = await Microsoft.Playwright.Playwright.CreateAsync();
        return await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }
}
