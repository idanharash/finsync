using System.Net;
using PuppeteerSharp;
using FinSync.Mizrahi;

namespace FinSync.Tests.Mizrahi.Browser;

[Trait("Category", "Browser")]
[Collection(ChromiumFixture.CollectionName)]
public class MizrahiLoginPageBrowserTests(ChromiumFixture chromium)
{
    private const string LoginPageUrl = "https://www.mizrahi-tefahot.co.il/login/index.html#/main/auth-page-he";
    private const string HomePageUrl = "https://www.mizrahi-tefahot.co.il/";
    private const string RejectedMessage = "פרטי ההזדהות שהזנת שגויים, באפשרותך לנסות שנית";

    private async Task<bool> CredentialsRejectedAtAsync(string url, string bodyText)
    {
        await using var page = await chromium.Browser.NewPageAsync();
        await page.SetRequestInterceptionAsync(true);
        page.Request += async (_, request) => await request.Request.RespondAsync(new ResponseData
        {
            Status = HttpStatusCode.OK,
            ContentType = "text/html; charset=utf-8",
            Body = $"""<!DOCTYPE html><html dir="rtl"><head><meta charset="utf-8"></head><body><p>{bodyText}</p></body></html>"""
        });

        await page.GoToAsync(url);

        return await MizrahiLoginPage.CredentialsRejectedAsync(page);
    }

    [Theory]
    [InlineData(LoginPageUrl, RejectedMessage, true)]
    [InlineData(LoginPageUrl, "כניסה לחשבונך", false)]
    [InlineData(HomePageUrl, RejectedMessage, false)]
    public async Task CredentialsRejectedAsync_DetectsOnlyTheBankRejectionPage(string url, string bodyText, bool expected)
    {
        Assert.Equal(expected, await CredentialsRejectedAtAsync(url, bodyText));
    }
}
