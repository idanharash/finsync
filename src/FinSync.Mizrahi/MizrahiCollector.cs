using System.Globalization;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.Mizrahi;

public sealed class MizrahiCollector(IOptions<MizrahiOptions> options, CollectionPeriod period)
    : ICollector<MizrahiTransaction>
{
    private static readonly TimeSpan LoginModalSettleDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan GridSettleDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan LoginPollInterval = TimeSpan.FromSeconds(1);
    private static readonly WaitForSelectorOptions Visible = new() { Visible = true };

    private const string BankDateFormat = "dd/MM/yyyy";

    private const string ClickScript = "selector => document.querySelector(selector).click()";

    private const string IsVisibleScript = """
        selector => {
            const element = document.querySelector(selector);
            return element !== null && element.getClientRects().length > 0;
        }
        """;

    private const string SetInputScript = """
        (selector, value) => {
            const input = document.querySelector(selector);
            input.value = value;
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        }
        """;

    private const string GridOrEmptyMessageScript = """
        (rowSelector, emptyMessageSelector) => {
            const emptyMessage = document.querySelector(emptyMessageSelector);
            return document.querySelectorAll(rowSelector).length > 0
                || (emptyMessage !== null && emptyMessage.getClientRects().length > 0);
        }
        """;

    private const string PeriodAppliedScript = """
        (summarySelector, from, to) => {
            const summary = document.querySelector(summarySelector);
            return summary !== null && summary.innerText.includes(from) && summary.innerText.includes(to);
        }
        """;

    public async Task<IReadOnlyList<MizrahiTransaction>> CollectAsync(CancellationToken ct)
    {
        var settings = options.Value;

        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = settings.Headless,
            DefaultViewport = null
        });

        var page = await browser.NewPageAsync();
        page.DefaultTimeout = PageTimeoutMilliseconds;

        await LogInAsync(page, settings, ct);
        var transactionsFrame = await OpenTransactionsAsync(page, ct);
        await ApplyPeriodAsync(transactionsFrame, period, ct);

        return await new MizrahiTransactionReader().ReadAsync(transactionsFrame, ct);
    }

    private static async Task LogInAsync(IPage page, MizrahiOptions settings, CancellationToken ct)
    {
        await page.GoToAsync(settings.LoginUrl);
        await page.WaitForSelectorAsync(MizrahiSelectors.OpenLoginButton);
        await ClickAsync(page.MainFrame, MizrahiSelectors.OpenLoginButton);

        var loginFrameElement = await page.WaitForSelectorAsync(MizrahiSelectors.LoginFrame);
        var loginFrame = await loginFrameElement.ContentFrameAsync();
        await loginFrame.WaitForSelectorAsync(MizrahiSelectors.UsernameInput, Visible);
        await Task.Delay(LoginModalSettleDelay, ct);

        await loginFrame.TypeAsync(MizrahiSelectors.UsernameInput, settings.Username);
        await loginFrame.TypeAsync(MizrahiSelectors.PasswordInput, settings.Password);
        await ClickAsync(loginFrame, MizrahiSelectors.SubmitLoginButton);

        await WaitForOnlineBankingAsync(page, TimeSpan.FromSeconds(settings.LoginTimeoutSeconds), ct);
    }

    private static async Task WaitForOnlineBankingAsync(IPage page, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!Uri.TryCreate(page.Url, UriKind.Absolute, out var url) || url.Host != MizrahiSelectors.OnlineBankingHost)
        {
            if (await MizrahiLoginPage.CredentialsRejectedAsync(page))
                throw new MizrahiPageException(
                    $"Login failed: the bank rejected the username or password. Check {MizrahiOptions.SectionName}:{nameof(MizrahiOptions.Username)} and {MizrahiOptions.SectionName}:{nameof(MizrahiOptions.Password)}.");

            if (DateTime.UtcNow > deadline)
                throw new MizrahiPageException(
                    "Login did not complete in time. Complete the verification step in the browser window.");

            await Task.Delay(LoginPollInterval, ct);
        }
    }

    private async Task<IFrame> OpenTransactionsAsync(IPage page, CancellationToken ct)
    {
        await page.WaitForSelectorAsync(MizrahiSelectors.CheckingAccountMenu, Visible);
        await ClickAsync(page.MainFrame, MizrahiSelectors.CheckingAccountMenu);
        await page.WaitForSelectorAsync(MizrahiSelectors.CheckingAccountMenuActive);

        await page.WaitForSelectorAsync(MizrahiSelectors.TransactionsMenu, Visible);
        await ClickAsync(page.MainFrame, MizrahiSelectors.TransactionsMenu);
        await page.WaitForSelectorAsync(MizrahiSelectors.TransactionsMenuActive);

        var frameElement = await page.WaitForSelectorAsync(MizrahiSelectors.TransactionsFrame);
        var frame = await frameElement.ContentFrameAsync();
        await frame.WaitForSelectorAsync(MizrahiSelectors.BetweenDatesButton, Visible);
        await WaitForGridOrEmptyMessageAsync(frame, ct);

        return frame;
    }

    private async Task ApplyPeriodAsync(IFrame frame, CollectionPeriod period, CancellationToken ct)
    {
        if (!await frame.EvaluateFunctionAsync<bool>(IsVisibleScript, MizrahiSelectors.FromDateInput))
            await ClickAsync(frame, MizrahiSelectors.BetweenDatesButton);

        await frame.WaitForSelectorAsync(MizrahiSelectors.FromDateInput, Visible);
        await frame.EvaluateFunctionAsync(SetInputScript, MizrahiSelectors.FromDateInput, ToBankDate(period.From));
        await frame.EvaluateFunctionAsync(SetInputScript, MizrahiSelectors.ToDateInput, ToBankDate(period.To));
        await ClickAsync(frame, MizrahiSelectors.ShowButton);

        await frame.WaitForFunctionAsync(
            PeriodAppliedScript,
            new WaitForFunctionOptions { Timeout = PageTimeoutMilliseconds },
            MizrahiSelectors.PeriodSummary,
            ToBankDate(period.From),
            ToBankDate(period.To));

        await WaitForGridOrEmptyMessageAsync(frame, ct);
    }

    private async Task WaitForGridOrEmptyMessageAsync(IFrame frame, CancellationToken ct)
    {
        await frame.WaitForFunctionAsync(
            GridOrEmptyMessageScript,
            new WaitForFunctionOptions { Timeout = PageTimeoutMilliseconds },
            MizrahiSelectors.GridRows,
            MizrahiSelectors.EmptyPeriodMessage);

        await Task.Delay(GridSettleDelay, ct);
    }

    private int PageTimeoutMilliseconds => (int)TimeSpan.FromSeconds(options.Value.PageTimeoutSeconds).TotalMilliseconds;

    private static Task ClickAsync(IFrame frame, string selector) =>
        frame.EvaluateFunctionAsync(ClickScript, selector);

    private static string ToBankDate(DateOnly date) => date.ToString(BankDateFormat, CultureInfo.InvariantCulture);
}
