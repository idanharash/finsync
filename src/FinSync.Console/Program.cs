using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.ConsoleHost;
using FinSync.IO.Json;
using FinSync.Mizrahi;
using FinSync.Masking;

Console.OutputEncoding = Encoding.UTF8;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var configuration = CompositionRoot.LoadConfiguration(AppContext.BaseDirectory);

    var services = new ServiceCollection();
    services.AddCollectionPeriod(configuration);
    services.AddMizrahiBank(configuration);
    services.AddJsonPersistence(configuration);
    services.AddTransactionMasking(configuration);
    services.AddTransient<TransactionJob>();

    using var provider = CompositionRoot.BuildValidatedProvider(services);
    provider.GetRequiredService<IStartupValidator>().Validate();

    var job = provider.GetRequiredService<TransactionJob>();
    var succeeded = await new TransactionJobRunner(Console.Out, Console.Error).RunAsync(job, cancellation.Token);

    Console.WriteLine(
        $"Output directory: {Path.GetFullPath(provider.GetRequiredService<IOptions<JsonPersistenceOptions>>().Value.OutputDirectory)}");
    if (succeeded)
        return ExitCodes.Success;

    Console.Error.WriteLine($"Failed providers: {job.ProviderName}");
    return ExitCodes.JobFailed;
}
catch (Exception exception) when (ValidationFailures(exception).Any())
{
    Console.Error.WriteLine("Configuration is invalid:");
    foreach (var failure in ValidationFailures(exception))
        Console.Error.WriteLine($"  {failure}");
    return ExitCodes.ConfigurationError;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return ExitCodes.Cancelled;
}

static IEnumerable<string> ValidationFailures(Exception exception) => exception switch
{
    OptionsValidationException validation => validation.Failures,
    AggregateException aggregate => aggregate.InnerExceptions.SelectMany(ValidationFailures),
    _ => []
};
