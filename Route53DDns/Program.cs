using System.Text.Json;
using Amazon;
using Amazon.Route53;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Route53DDns;

var serviceCollection = new ServiceCollection();
ConfigureServices(serviceCollection);

var serviceProvider = serviceCollection.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    logger.LogInformation("Cancelamento recebido. Encerrando de forma graciosa...");
    cts.Cancel();
};

try
{
    var updater = serviceProvider.GetRequiredService<DnsUpdater>();
    await updater.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    logger.LogInformation("Execução encerrada pelo usuário.");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Erro fatal durante a execução.");
}

return;

void ConfigureServices(IServiceCollection services)
{
    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
            options.SingleLine = true;
            options.IncludeScopes = true;
        });
        
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("System.Net.Http.HttpClient.IExternalIpService.ClientHandler", LogLevel.Warning);
        builder.AddFilter("System.Net.Http.HttpClient.IExternalIpService.LogicalHandler", LogLevel.Warning);
    });

    var configPath = "config.json";
    Config? config = null;
    if (File.Exists(configPath))
        try
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao ler config.json: {ex.Message}");
        }

    // Fallback if config is null
    config ??= new Config(null, null, null, null, 60);

    // Priority: Environment Variables > config.json (for credentials)
    var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? config.AccessKey;
    var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? config.SecretKey;

    var finalConfig = config with { AccessKey = accessKey, SecretKey = secretKey };
    services.AddSingleton(finalConfig);

    services.AddSingleton<IAmazonRoute53>(_ =>
    {
        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            return new AmazonRoute53Client(new BasicAWSCredentials(accessKey, secretKey), RegionEndpoint.USEast1);

        // Use default chain if no explicit credentials
        return new AmazonRoute53Client(RegionEndpoint.USEast1);
    });

    services.AddHttpClient<IExternalIpService, ExternalIpService>()
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    services.AddSingleton<IRoute53Service, Route53Service>();
    services.AddSingleton<DnsUpdater>();
}