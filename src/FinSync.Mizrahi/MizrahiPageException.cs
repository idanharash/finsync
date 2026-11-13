namespace FinSync.Mizrahi;

public sealed class MizrahiPageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
