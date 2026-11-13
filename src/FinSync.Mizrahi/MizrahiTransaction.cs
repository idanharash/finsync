namespace FinSync.Mizrahi;

public sealed record MizrahiTransaction(
    string Date,
    string Amount,
    string Description,
    string OriginalReference,
    string TransactionNumber,
    IReadOnlyList<MizrahiTransactionDetail> Details);
