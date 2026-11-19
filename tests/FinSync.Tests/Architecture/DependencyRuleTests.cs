using System.Xml.Linq;
using FinSync.Domain;
using FinSync.IO.Json;
using FinSync.Mizrahi;

namespace FinSync.Tests.Architecture;

public class DependencyRuleTests
{
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["FinSync.Core"] = [],
        ["FinSync.Mizrahi"] = ["FinSync.Core"],
        ["FinSync.IO.Json"] = ["FinSync.Core"],
        ["FinSync.Console"] = ["FinSync.Core", "FinSync.Mizrahi", "FinSync.IO.Json"]
    };

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinSync.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("FinSync.sln was not found above the test output directory.");
    }

    private static XDocument ProjectFile(string project) =>
        XDocument.Load(Path.Combine(SolutionDirectory(), "src", project, $"{project}.csproj"));

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        ProjectFile(project)
            .Descendants("ProjectReference")
            // csproj paths use Windows separators; normalize so this also works on Linux CI.
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();

    [Fact]
    public void Projects_ReferenceExactlyTheirAllowedProjects()
    {
        var violations = AllowedProjectReferences
            .Where(rule => !rule.Value.Order().SequenceEqual(ProjectReferences(rule.Key).Order()))
            .Select(rule => $"{rule.Key}: {string.Join(", ", ProjectReferences(rule.Key))}");

        Assert.Empty(violations);
    }

    [Fact]
    public void BankCollectionAndPersistence_NeverDependOnEachOther()
    {
        var mizrahi = typeof(MizrahiAdapter).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);
        var persistence = typeof(JsonTransactionWriter).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain("FinSync.IO.Json", mizrahi);
        Assert.DoesNotContain("FinSync.Mizrahi", persistence);
    }

    [Fact]
    public void Core_HasNoBrowser()
    {
        Assert.DoesNotContain(
            ProjectFile("FinSync.Core").Descendants("PackageReference"),
            package => package.Attribute("Include")!.Value == "PuppeteerSharp");
        Assert.DoesNotContain(
            typeof(Transaction).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "PuppeteerSharp");
    }
}
