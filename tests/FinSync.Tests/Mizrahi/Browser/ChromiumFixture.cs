using PuppeteerSharp;

namespace FinSync.Tests.Mizrahi.Browser;

public sealed class ChromiumFixture : IAsyncLifetime
{
    public const string CollectionName = "Chromium";

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await new BrowserFetcher().DownloadAsync();
        Browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();
    }
}

[CollectionDefinition(ChromiumFixture.CollectionName)]
public sealed class ChromiumCollection : ICollectionFixture<ChromiumFixture>
{
}
