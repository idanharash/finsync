using System.Net;
using System.Text;
using System.Text.Json;

namespace FinSync.Tests.Mizrahi.Browser;

public sealed class SyntheticTransactionsPage
{
    private readonly List<string> _rows = [];
    private bool _emptyPeriodMessageVisible;
    private int? _totalPages = 1;

    public SyntheticTransactionsPage WithGroupHeader(string text)
    {
        _rows.Add($"""<tr class="k-master-row"><td class="seperate-td">{Encode(text)}</td></tr>""");
        return this;
    }

    public SyntheticTransactionsPage WithTransaction(
        string date,
        decimal amount,
        string description,
        string originalReference,
        string transactionNumber,
        params (string Label, string Value)[] details)
    {
        var dataItem = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["MC02PeulaTaaEZ"] = date,
            ["MC02SchumEZ"] = amount,
            ["MC02TnuaTeurEZ"] = description,
            ["MC02AsmahtaMekoritEZ"] = originalReference,
            ["TransactionNumber"] = transactionNumber
        });

        var detailsMarkup = details.Length == 0 ? null : DetailMarkup(details);
        _rows.Add(TransactionRowMarkup(dataItem, description, amount, detailsMarkup));
        return this;
    }

    public SyntheticTransactionsPage WithTransactionWithUnsupportedDetails(string description)
    {
        var dataItem = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["MC02PeulaTaaEZ"] = "2026-10-01T00:00:00",
            ["MC02SchumEZ"] = -1m,
            ["MC02TnuaTeurEZ"] = description,
            ["MC02AsmahtaMekoritEZ"] = "1",
            ["TransactionNumber"] = "1"
        });

        var unsupportedDetails = """<td class="k-detail-cell"><div>פרטי שיק ללא מבנה מוכר</div></td>""";
        _rows.Add(TransactionRowMarkup(dataItem, description, -1m, unsupportedDetails));
        return this;
    }

    public SyntheticTransactionsPage WithUnknownRow()
    {
        _rows.Add("""<tr class="k-master-row"><td class="something-new">?</td></tr>""");
        return this;
    }

    public SyntheticTransactionsPage WithEmptyPeriodMessage()
    {
        _emptyPeriodMessageVisible = true;
        return this;
    }

    public SyntheticTransactionsPage WithTotalPages(int totalPages)
    {
        _totalPages = totalPages;
        return this;
    }

    public SyntheticTransactionsPage WithoutGridWidget()
    {
        _totalPages = null;
        return this;
    }

    public string ToHtml()
    {
        var gridWidget = _totalPages is { } pages
            ? $$"""
                window.jQuery = element => ({
                    data: name => name === 'kendoGrid' && element && element.id === 'grid'
                        ? { dataSource: { totalPages: () => {{pages}} } }
                        : undefined
                });
                """
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html dir="rtl">
            <body>
              <div class="row-sicum">תנועות בחשבון מתאריך 01/10/2026 עד 31/10/2026.</div>
              <div ng-show="vm.isGridEmpty && !vm.messages" style="display: {{(_emptyPeriodMessageVisible ? "block" : "none")}}">
                לא נמצאו תנועות לפי החיפוש.
              </div>
              <div id="grid">
                <table><tbody>
                  {{string.Join(Environment.NewLine, _rows)}}
                </tbody></table>
              </div>
              <script>
                window.angular = {
                  element: element => ({
                    scope: () => element.dataset.item ? { dataItem: JSON.parse(element.dataset.item) } : undefined
                  })
                };
                {{gridWidget}}
                document.querySelectorAll('a.expandItem').forEach(arrow => arrow.addEventListener('click', event => {
                  event.preventDefault();
                  const row = arrow.closest('tr');
                  setTimeout(() => row.insertAdjacentHTML('afterend', '<tr class="k-detail-row">' + row.dataset.details + '</tr>'), 50);
                }));
              </script>
            </body>
            </html>
            """;
    }

    private static string TransactionRowMarkup(string? dataItem, string description, decimal amount, string? detailsMarkup)
    {
        var dataAttributes = new StringBuilder();
        if (dataItem is not null)
            dataAttributes.Append($" data-item=\"{Encode(dataItem)}\"");
        if (detailsMarkup is not null)
            dataAttributes.Append($" data-details=\"{Encode(detailsMarkup)}\"");

        var arrow = detailsMarkup is null ? string.Empty : """<a href="" class="k-icon k-i-expand expandItem"></a>""";

        return $"""
            <tr class="k-master-row"{dataAttributes}>
              <td>01/10/26</td>
              <td class="h-cheque">{Encode(description)}</td>
              <td class="directionSwitch">{amount}</td>
              <td class="directionSwitch">1,000.00</td>
              <td class="k-hierarchy-cell h-expend">{arrow}</td>
            </tr>
            """;
    }

    private static string DetailMarkup((string Label, string Value)[] details)
    {
        var pairs = details.Select(detail =>
            $"""<div class="table-cell"><div><span>{Encode(detail.Label)}</span> <span>{Encode(detail.Value)}</span></div></div>""");

        return $"""<td class="k-hierarchy-cell"></td><td class="k-detail-cell"><div class="inline-table">{string.Concat(pairs)}</div></td>""";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
