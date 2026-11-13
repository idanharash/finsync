using System.Globalization;
using PuppeteerSharp;

namespace FinSync.Mizrahi;

public sealed class MizrahiTransactionReader
{
    private const string TransactionRow = "transaction";
    private const string GroupHeaderRow = "header";
    private const string RowWithoutData = "missing-data";
    private static readonly TimeSpan DetailsTimeout = TimeSpan.FromSeconds(15);

    private const string PageStateScript = """
        (rowSelector, emptyMessageSelector) => {
            const emptyMessage = document.querySelector(emptyMessageSelector);
            return [
                String(document.querySelectorAll(rowSelector).length),
                emptyMessage !== null && emptyMessage.getClientRects().length > 0 ? '1' : '0'
            ];
        }
        """;

    private const string PageCountScript = """
        gridSelector => {
            const grid = document.querySelector(gridSelector);
            const widget = grid && window.jQuery ? window.jQuery(grid).data('kendoGrid') : null;
            return widget ? widget.dataSource.totalPages() : -1;
        }
        """;

    private const string RowSnapshotScript = """
        (row, amountSelector, headerSelector, arrowSelector) => {
            if (!row.querySelector(amountSelector))
                return [row.querySelector(headerSelector) ? 'header' : 'unknown'];

            const scope = window.angular ? window.angular.element(row).scope() : null;
            const item = scope ? scope.dataItem : null;
            if (!item)
                return ['missing-data'];

            const text = value => value === null || value === undefined ? '' : String(value);
            return [
                'transaction',
                text(item.MC02PeulaTaaEZ),
                text(item.MC02SchumEZ),
                text(item.MC02TnuaTeurEZ),
                text(item.MC02AsmahtaMekoritEZ),
                text(item.TransactionNumber),
                row.querySelector(arrowSelector) ? '1' : '0'
            ];
        }
        """;

    private const string ExpandScript = "(row, arrowSelector) => row.querySelector(arrowSelector).click()";

    private const string DetailsLoadedScript = """
        (row, detailRowClass) => {
            const detailRow = row.nextElementSibling;
            return detailRow !== null
                && detailRow.classList.contains(detailRowClass)
                && detailRow.innerText.trim().length > 0;
        }
        """;

    private const string DetailPairsScript = """
        (row, pairSelector) => [...row.nextElementSibling.querySelectorAll(pairSelector)]
            .map(pair => [...pair.querySelectorAll('span')].map(span => span.innerText.trim()))
        """;

    public async Task<IReadOnlyList<MizrahiTransaction>> ReadAsync(IFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ct.ThrowIfCancellationRequested();

        var state = await frame.EvaluateFunctionAsync<string[]>(
            PageStateScript, MizrahiSelectors.GridRows, MizrahiSelectors.EmptyPeriodMessage);
        var rowCount = int.Parse(state[0], CultureInfo.InvariantCulture);
        var emptyPeriodShown = state[1] == "1";

        if (rowCount == 0 && emptyPeriodShown)
            return [];
        if (rowCount == 0)
            throw new MizrahiPageException("The transactions grid has no rows and the empty-period message is not shown.");
        if (emptyPeriodShown)
            throw new MizrahiPageException("The transactions grid shows rows together with the empty-period message.");

        await EnsureSinglePageAsync(frame);

        var rows = await frame.QuerySelectorAllAsync(MizrahiSelectors.GridRows);
        var transactions = new List<MizrahiTransaction>();

        for (var index = 0; index < rows.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var position = index + 1;
            var snapshot = await rows[index].EvaluateFunctionAsync<string[]>(
                RowSnapshotScript, MizrahiSelectors.AmountCell, MizrahiSelectors.GroupHeaderCell, MizrahiSelectors.ExpandArrow);

            switch (snapshot[0])
            {
                case GroupHeaderRow:
                    continue;
                case TransactionRow:
                    transactions.Add(await ReadTransactionAsync(frame, rows[index], snapshot, position));
                    break;
                case RowWithoutData:
                    throw new MizrahiPageException($"Transaction row {position} has no underlying data.");
                default:
                    throw new MizrahiPageException($"Row {position} has an unexpected structure.");
            }
        }

        return transactions;
    }

    private static async Task EnsureSinglePageAsync(IFrame frame)
    {
        var pageCount = await frame.EvaluateFunctionAsync<int>(PageCountScript, MizrahiSelectors.Grid);

        if (pageCount < 0)
            throw new MizrahiPageException("The transactions grid widget was not found.");
        if (pageCount > 1)
            throw new MizrahiPageException($"The transactions span {pageCount} pages; pagination is not supported.");
    }

    private static async Task<MizrahiTransaction> ReadTransactionAsync(
        IFrame frame,
        IElementHandle row,
        string[] snapshot,
        int position)
    {
        var hasDetails = snapshot[6] == "1";
        IReadOnlyList<MizrahiTransactionDetail> details = hasDetails ? await ReadDetailsAsync(frame, row, position) : [];

        return new MizrahiTransaction(
            Date: snapshot[1],
            Amount: snapshot[2],
            Description: snapshot[3],
            OriginalReference: snapshot[4],
            TransactionNumber: snapshot[5],
            Details: details);
    }

    private static async Task<IReadOnlyList<MizrahiTransactionDetail>> ReadDetailsAsync(
        IFrame frame,
        IElementHandle row,
        int position)
    {
        await row.EvaluateFunctionAsync(ExpandScript, MizrahiSelectors.ExpandArrow);

        try
        {
            await frame.WaitForFunctionAsync(
                DetailsLoadedScript,
                new WaitForFunctionOptions { Timeout = (int)DetailsTimeout.TotalMilliseconds },
                row,
                MizrahiSelectors.DetailRowClass);
        }
        catch (WaitTaskTimeoutException exception)
        {
            throw new MizrahiPageException($"The details of transaction row {position} did not load.", exception);
        }

        var pairs = await row.EvaluateFunctionAsync<string[][]>(DetailPairsScript, MizrahiSelectors.DetailPair);

        if (pairs.Length == 0 || pairs.Any(pair => pair.Length != 2))
            throw new MizrahiPageException($"The details of transaction row {position} have an unsupported format.");

        return pairs.Select(pair => new MizrahiTransactionDetail(pair[0], pair[1])).ToList();
    }
}
