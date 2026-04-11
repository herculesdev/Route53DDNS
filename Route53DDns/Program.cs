using Amazon;
using Amazon.Route53;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Route53DDns;
using Route53DDns.Application;
using Route53DDns.Configuration;
using Route53DDns.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AwsConfig>(builder.Configuration.GetSection("AwsConfig"));

builder.Services.AddLogging(logging =>
{
    logging.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
        options.SingleLine = true;
        options.IncludeScopes = true;
    });
    logging.AddFilter("System.Net.Http.HttpClient.IExternalIpService.ClientHandler", LogLevel.Warning);
    logging.AddFilter("System.Net.Http.HttpClient.IExternalIpService.LogicalHandler", LogLevel.Warning);
});

builder.Services.AddSingleton<IAmazonRoute53>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AwsConfig>>().Value;

    if (!string.IsNullOrEmpty(config.AccessKey) && !string.IsNullOrEmpty(config.SecretKey))
        return new AmazonRoute53Client(new BasicAWSCredentials(config.AccessKey, config.SecretKey),
            RegionEndpoint.USEast1);

    return new AmazonRoute53Client(RegionEndpoint.USEast1);
});

builder.Services.AddHttpClient<IExternalIpService, ExternalIpService>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddSingleton<IRoute53Service, Route53Service>();
builder.Services.AddHostedService<DnsUpdater>();

var host = builder.Build();
await host.RunAsync();