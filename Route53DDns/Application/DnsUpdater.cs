using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Route53DDns.Configuration;
using Route53DDns.Services;

namespace Route53DDns.Application;

public class DnsUpdater(
    IExternalIpService ipService,
    IRoute53Service route53Service,
    IOptions<AwsConfig> configOptions,
    ILogger<DnsUpdater> logger) : BackgroundService
{
    private readonly AwsConfig _awsConfig = configOptions.Value;
    private string? _lastExternalIp;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting DnsUpdater...");
        _awsConfig.Validate();
        var hostedZone = await route53Service.FindHostedZoneAsync(_awsConfig.TargetHostedZone!, ct);
        if (hostedZone == null)
        {
            logger.LogCritical("Target hosted zone {Target} not found in Route 53.", _awsConfig.TargetHostedZone);
            return;
        }

        logger.LogInformation("Hosted zone found: {Name} (ID: {Id})", hostedZone.Name, hostedZone.Id);
        var records = await route53Service.ListRecordsAsync(hostedZone.Id, ct);
        if (records.Count > 0)
        {
            var maxNameLength = records.Max(x => x.Name.Length);
            foreach (var record in records)
            {
                var values = string.Join(", ", record.ResourceRecords.Select(r => r.Value));
                logger.LogInformation("{Type,5} | {Name} -> {Values}", record.Type, record.Name.PadRight(maxNameLength),
                    values);
            }
        }
        else
        {
            logger.LogInformation("No records found in the zone.");
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var externalIp = await ipService.GetExternalIpAsync(ct);
                if (externalIp == null)
                {
                    logger.LogWarning("Could not get external IP. Retrying in {Interval}s.", _awsConfig.Interval);
                }
                else if (externalIp != _lastExternalIp)
                {
                    logger.LogInformation("IP change detected: [{OldIp}] -> [{NewIp}]", _lastExternalIp ?? "N/A",
                        externalIp);
                    await route53Service.UpdateRecordsAsync(hostedZone.Id, externalIp, _awsConfig.TargetRecords!, ct);
                    _lastExternalIp = externalIp;
                    logger.LogInformation("Records updated successfully.");
                }
                else
                {
                    logger.LogInformation("External IP has not changed: [{Ip}]", externalIp);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in update loop.");
            }

            try
            {
                await Task.Delay(_awsConfig.Interval * 1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("DnsUpdater stopped.");
    }
}