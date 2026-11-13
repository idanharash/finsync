namespace FinSync.Mizrahi;

public sealed class MizrahiOptions
{
    public const string SectionName = "MizrahiBank";

    public string LoginUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int LoginTimeoutSeconds { get; set; }

    public int PageTimeoutSeconds { get; set; }

    public bool Headless { get; set; }

    public override string ToString() => nameof(MizrahiOptions);
}
