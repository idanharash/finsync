using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FinSync.Abstractions;
using FinSync.Application;
using FinSync.ConsoleHost;
using FinSync.Shared;
using FinSync.Registration;
using FinSync.Tests.Application;

namespace FinSync.Tests.ConsoleHost;

public class ApplicationRegistrationTests
{
    private const string FakeProviderName = "FakeProvider";

    private static readonly IConfiguration ShippedConfiguration = ShippedSettings.Configuration();

    private static IServiceCollection SharedServices() =>
        new ServiceCollection()
            .AddTransactionMasking(ShippedConfiguration)
            .AddSingleton<ITransactionWriter, FakeWriter>();

    private static IServiceCollection WithFakeProvider(IServiceCollection services) =>
        services.AddProvider<FakeRaw, FakeCollector, FakeAdapter, FakeMasker>(FakeProviderName);

    private static IServiceCollection WithOtherProvider(IServiceCollection services) =>
        services.AddProvider<OtherRaw, OtherCollector, OtherAdapter, OtherMasker>("OtherProvider");

    private static ServiceProvider BuildValidated(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    [Fact]
    public void CompleteProvider_ResolvesOneTypedJobWithProviderName()
    {
        using var provider = BuildValidated(WithFakeProvider(SharedServices()).AddApplication());

        var job = Assert.Single(provider.GetServices<ITransactionJob>());
        Assert.IsType<TransactionJob<FakeRaw>>(job);
        Assert.Equal(FakeProviderName, job.ProviderName);
    }

    [Fact]
    public void TwoProviders_ResolveOneJobAndOwnMaskerEach()
    {
        using var provider = BuildValidated(WithOtherProvider(WithFakeProvider(SharedServices())).AddApplication());

        Assert.Equal(
            new[] { typeof(TransactionJob<FakeRaw>), typeof(TransactionJob<OtherRaw>) },
            provider.GetServices<ITransactionJob>().Select(job => job.GetType()).OrderBy(type => type.FullName));
        Assert.IsType<FakeMasker>(provider.GetRequiredService<ITransactionMasker<FakeRaw>>());
        Assert.IsType<OtherMasker>(provider.GetRequiredService<ITransactionMasker<OtherRaw>>());
    }

    [Fact]
    public void RepeatedRegistration_ProducesOneJob()
    {
        var services = WithFakeProvider(WithFakeProvider(SharedServices())).AddApplication().AddApplication();

        using var provider = BuildValidated(services);

        Assert.Single(provider.GetServices<ITransactionJob>());
    }

    [Fact]
    public void AddApplication_WithoutProviders_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SharedServices().AddApplication());

        Assert.Contains("No transaction provider", exception.Message);
    }

    [Fact]
    public void AddProvider_AfterAddApplication_Throws()
    {
        var services = WithFakeProvider(SharedServices()).AddApplication();

        var exception = Assert.Throws<InvalidOperationException>(() => WithOtherProvider(services));

        Assert.Contains("before AddApplication()", exception.Message);
    }

    [Theory]
    [InlineData("ICollector<FakeRaw>")]
    [InlineData("IAdapter<FakeRaw>")]
    [InlineData("ITransactionMasker<FakeRaw>")]
    public void AddApplication_ProviderMissingComponent_Throws(string missingComponent)
    {
        var services = SharedServices().AddSingleton<IProviderRegistration>(new ProviderRegistration<FakeRaw>(FakeProviderName));
        if (missingComponent != "ICollector<FakeRaw>")
            services.AddTransient<ICollector<FakeRaw>, FakeCollector>();
        if (missingComponent != "IAdapter<FakeRaw>")
            services.AddTransient<IAdapter<FakeRaw>, FakeAdapter>();
        if (missingComponent != "ITransactionMasker<FakeRaw>")
            services.AddTransient<ITransactionMasker<FakeRaw>, FakeMasker>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddApplication());

        Assert.Contains($"{missingComponent} is not registered", exception.Message);
    }

    [Fact]
    public void AddApplication_ConflictingProviderMaskers_Throws()
    {
        var services = WithFakeProvider(SharedServices()).AddTransient<ITransactionMasker<FakeRaw>, AlternativeFakeMasker>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddApplication());

        Assert.Contains("conflicting registrations for ITransactionMasker<FakeRaw>", exception.Message);
    }

    [Fact]
    public void BuildValidated_MissingSharedMaskerOrWriter_Throws()
    {
        var withoutSharedMasker = WithFakeProvider(new ServiceCollection().AddSingleton<ITransactionWriter, FakeWriter>()).AddApplication();
        var withoutWriter = WithFakeProvider(new ServiceCollection().AddTransactionMasking(ShippedConfiguration)).AddApplication();

        Assert.Throws<AggregateException>(() => BuildValidated(withoutSharedMasker));
        Assert.Throws<AggregateException>(() => BuildValidated(withoutWriter));
    }
}
