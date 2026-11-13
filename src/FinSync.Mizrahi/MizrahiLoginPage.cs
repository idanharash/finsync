using PuppeteerSharp;

namespace FinSync.Mizrahi;

public static class MizrahiLoginPage
{
    private const string ContainsTextScript = "text => document.body !== null && document.body.innerText.includes(text)";

    public static async Task<bool> CredentialsRejectedAsync(IPage page)
    {
        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out var url)
            || !url.AbsolutePath.StartsWith(MizrahiSelectors.LoginPagePath, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return await page.EvaluateFunctionAsync<bool>(ContainsTextScript, MizrahiSelectors.RejectedCredentialsText);
        }
        catch (PuppeteerException)
        {
            return false;
        }
    }
}
