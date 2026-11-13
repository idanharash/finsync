using FinSync.Domain;

namespace FinSync.Abstractions;

public interface ITransactionMasker<TRaw>
{
    MaskedDescription Mask(TRaw rawTransaction);
}
