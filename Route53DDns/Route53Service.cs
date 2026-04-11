using Amazon.Route53;
using Amazon.Route53.Model;
using Microsoft.Extensions.Logging;

namespace Route53DDns;

public interface IRoute53Service
{
    Task<HostedZone?> FindHostedZoneAsync(string zoneName, CancellationToken ct);
    Task<List<ResourceRecordSet>> ListRecordsAsync(string hostedZoneId, CancellationToken ct);
    Task UpdateRecordsAsync(string hostedZoneId, string ip, TargetRecordConfig[] targets, CancellationToken ct);
}

public class Route53Service(IAmazonRoute53 route53Client, ILogger<Route53Service> logger) : IRoute53Service
{
    public async Task<HostedZone?> FindHostedZoneAsync(string zoneName, CancellationToken ct)
    {
        var normalizedName = zoneName.Trim().TrimEnd('.').ToLowerInvariant() + ".";
        var request = new ListHostedZonesRequest();

        do
        {
            var response = await route53Client.ListHostedZonesAsync(request, ct);
            var zone = response.HostedZones.FirstOrDefault(z => z.Name.ToLowerInvariant() == normalizedName);
            if (zone != null) return zone;

            request.Marker = response.NextMarker;
        } while (!string.IsNullOrEmpty(request.Marker));

        return null;
    }

    public async Task<List<ResourceRecordSet>> ListRecordsAsync(string hostedZoneId, CancellationToken ct)
    {
        var records = new List<ResourceRecordSet>();
        var request = new ListResourceRecordSetsRequest(hostedZoneId);

        do
        {
            var response = await route53Client.ListResourceRecordSetsAsync(request, ct);
            records.AddRange(response.ResourceRecordSets);

            request.StartRecordName = response.NextRecordName;
            request.StartRecordType = response.NextRecordType;
            request.StartRecordIdentifier = response.NextRecordIdentifier;
        } while (request.StartRecordName != null);

        return records;
    }

    public async Task UpdateRecordsAsync(string hostedZoneId, string ip, TargetRecordConfig[] targets,
        CancellationToken ct)
    {
        var changes = targets.Select(t => new Change
        {
            Action = ChangeAction.UPSERT,
            ResourceRecordSet = new ResourceRecordSet
            {
                Name = t.Name,
                Type = RRType.FindValue(t.Type),
                TTL = 60,
                ResourceRecords = [new ResourceRecord { Value = ip }]
            }
        }).ToList();

        var request = new ChangeResourceRecordSetsRequest(hostedZoneId, new ChangeBatch(changes));
        await route53Client.ChangeResourceRecordSetsAsync(request, ct);
        logger.LogInformation("Solicitação de alteração enviada ao Route 53 para {Count} registros.", changes.Count);
    }
}