using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.IO.Json;
using FinSync.Tests.Application;

namespace FinSync.Tests.IO;

public class JsonPersistenceRegistrationTests
{
    private static IConfiguration Configuration(string? outputDirectory) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JsonPersistence:OutputDirectory"] = outputDirectory })
            .Build();

    [Fact]
    public void AddJsonPersistence_MissingOutputDirectory_FailsStartupValidation()
    {
        using var provider = new ServiceCollection()
            .AddJsonPersistence(Configuration(" "))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("JsonPersistence:OutputDirectory is required", exception.ToString());
    }

    [Fact]
    public void AddJsonPersistence_AnotherWriterAlreadyRegistered_Throws()
    {
        var services = new ServiceCollection().AddSingleton<ITransactionWriter, FakeWriter>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddJsonPersistence(Configuration("output")));

        Assert.Contains("Another transaction writer", exception.Message);
    }
}
