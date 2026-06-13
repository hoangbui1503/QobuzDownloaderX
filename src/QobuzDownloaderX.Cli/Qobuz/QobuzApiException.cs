namespace QobuzDownloaderX.Cli.Qobuz;

public sealed class QobuzApiException : Exception
{
    public int? StatusCode { get; }

    public QobuzApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
