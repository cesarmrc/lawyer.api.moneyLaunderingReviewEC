using Microsoft.Playwright;

namespace SecureHumanLoopCaptcha.Worker.Automation;

public static class CaptchaDetector
{
    private static readonly string[] CaptchaSelectors =
    {
        "iframe[src*='recaptcha']",
        ".g-recaptcha",
        "[data-sitekey]",
        "text=I'm not a robot",
        "text=verify you are human",
        "text=select all images"
    };

    public static async Task<bool> DetectAsync(IPage page)
    {
        foreach (var selector in CaptchaSelectors)
        {
            var element = await page.QuerySelectorAsync(selector);
            if (element is not null)
            {
                return true;
            }
        }

        var content = await page.ContentAsync();
        return content.Contains("I'm not a robot", StringComparison.OrdinalIgnoreCase)
            || content.Contains("verify you are human", StringComparison.OrdinalIgnoreCase);
    }
}
