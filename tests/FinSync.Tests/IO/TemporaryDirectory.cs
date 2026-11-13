namespace FinSync.Tests.IO;

public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "finsync-tests", Guid.NewGuid().ToString("N"));

    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (!Directory.Exists(Path))
            return;

        foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            System.IO.File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(Path, recursive: true);
    }
}
