using System.Reflection;
using System.Xml.Linq;
using FinSync.Abstractions;
using FinSync.Application;
using FinSync.ConsoleHost;
using FinSync.Domain;
using FinSync.IO.Json;
using FinSync.Shared;
using FinSync.Mizrahi;
using FinSync.Registration;

namespace FinSync.Tests.Architecture;

public class DependencyRuleTests
{
    private static readonly string[] IndependentModules = ["FinSync.Application", "FinSync.Mizrahi", "FinSync.IO.Json", "FinSync.Shared"];

    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["FinSync.Domain"] = [],
        ["FinSync.Abstractions"] = ["FinSync.Domain"],
        ["FinSync.Registration"] = ["FinSync.Abstractions"],
        ["FinSync.Application"] = ["FinSync.Abstractions"],
        ["FinSync.Mizrahi"] = ["FinSync.Abstractions", "FinSync.Registration"],
        ["FinSync.IO.Json"] = ["FinSync.Abstractions"],
        ["FinSync.Shared"] = ["FinSync.Abstractions"]
    };

    private static readonly Dictionary<string, Assembly> Assemblies = new()
    {
        ["FinSync.Domain"] = typeof(Transaction).Assembly,
        ["FinSync.Abstractions"] = typeof(ICollector<>).Assembly,
        ["FinSync.Registration"] = typeof(ProviderRegistration<>).Assembly,
        ["FinSync.Application"] = typeof(ITransactionJob).Assembly,
        ["FinSync.Mizrahi"] = typeof(MizrahiAdapter).Assembly,
        ["FinSync.IO.Json"] = typeof(JsonTransactionWriter).Assembly,
        ["FinSync.Shared"] = typeof(SensitiveDataMasker).Assembly,
        ["FinSync.Console"] = typeof(CompositionRoot).Assembly
    };

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinSync.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("FinSync.sln was not found above the test output directory.");
    }

    private static string ProjectDirectory(string project) => Path.Combine(SolutionDirectory(), "src", project);

    private static XDocument ProjectFile(string project) =>
        XDocument.Load(Path.Combine(ProjectDirectory(project), $"{project}.csproj"));

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        ProjectFile(project)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .ToList();

    private static HashSet<string> TransitiveProjectReferences(string project)
    {
        var visited = new HashSet<string>();
        var pending = new Stack<string>(ProjectReferences(project));

        while (pending.TryPop(out var next))
        {
            if (!visited.Add(next))
                continue;
            foreach (var reference in ProjectReferences(next))
                pending.Push(reference);
        }

        return visited;
    }

    private static IReadOnlyList<string> AssemblyReferences(string project) =>
        Assemblies[project].GetReferencedAssemblies().Select(reference => reference.Name!).ToList();

    [Fact]
    public void Projects_ReferenceExactlyTheirAllowedProjects()
    {
        var violations = AllowedProjectReferences
            .Where(rule => !rule.Value.Order().SequenceEqual(ProjectReferences(rule.Key).Order()))
            .Select(rule => $"{rule.Key}: {string.Join(", ", ProjectReferences(rule.Key))}");

        Assert.Empty(violations);
    }

    [Fact]
    public void IndependentModules_NeverDependOnEachOtherDirectlyTransitivelyOrByAssembly()
    {
        var violations =
            from module in IndependentModules
            from other in IndependentModules
            where other != module
                && (TransitiveProjectReferences(module).Contains(other) || AssemblyReferences(module).Contains(other))
            select $"{module} -> {other}";

        Assert.Empty(violations);
    }

    [Fact]
    public void DomainAndAbstractions_ArePure_AndApplicationHasNoDependencyInjection()
    {
        Assert.Empty(ProjectFile("FinSync.Domain").Descendants("PackageReference"));
        Assert.Empty(ProjectFile("FinSync.Abstractions").Descendants("PackageReference"));
        Assert.Empty(Assemblies["FinSync.Abstractions"].GetExportedTypes().Where(type => !type.IsInterface).Select(type => type.FullName));
        Assert.DoesNotContain(
            AssemblyReferences("FinSync.Application"),
            reference => reference.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsoleSource_DoesNotNameConcreteProviderTypes()
    {
        string[] concreteProviderTypes = ["MizrahiCollector", "MizrahiAdapter", "MizrahiTransactionMasker", "MizrahiTransaction"];
        var consoleSource = Directory
            .EnumerateFiles(ProjectDirectory("FinSync.Console"), "*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText);

        Assert.All(consoleSource, source =>
            Assert.DoesNotContain(concreteProviderTypes, typeName => source.Contains(typeName, StringComparison.Ordinal)));
    }
}
