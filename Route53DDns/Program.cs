// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Text.Json;
using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using Route53DDns;

Console.Write("Initializing...");
var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
if(config is null)
{
    Console.WriteLine("Failed. Config not found");
    return;
}

HostedZone? awsTargetHostedZone = null;

using var awsRoute53Client = new AmazonRoute53Client(new BasicAWSCredentials(config.AccessKey, config.SecretKey), RegionEndpoint.USEast1);
Console.WriteLine("OK!");

Console.Write("Retrieving hosted zones...");
var awsHostedZonesResponse = await awsRoute53Client.ListHostedZonesAsync();
Console.WriteLine("OK!");

foreach (var awsHostedZone in awsHostedZonesResponse.HostedZones)
{
    if (config.TargetHostedZone+"." == awsHostedZone.Name)
        awsTargetHostedZone = awsHostedZone;
    
    Console.WriteLine(awsHostedZone.Name);
}

if (awsTargetHostedZone is null)
{
    Console.WriteLine($"Target hosted zone {config.TargetHostedZone} not found. Exiting...");
    return;
}

Console.Write("Target hosted zone found. Retrieving records...");
var awsRecordResponse = await awsRoute53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest(awsTargetHostedZone.Id));
Console.WriteLine("OK!");
var maxNameLength = awsRecordResponse.ResourceRecordSets.Max(x => x.Name.Length);
foreach (var awsHostedZoneRecord in awsRecordResponse.ResourceRecordSets)
{
    if (awsHostedZoneRecord.ResourceRecords.Count > 1)
    {
        Console.WriteLine($"{awsHostedZoneRecord.Type,5} | {awsHostedZoneRecord.Name.PadRight(maxNameLength, ' ')}");
        awsHostedZoneRecord.ResourceRecords.ForEach(r => Console.WriteLine($"         ->{r.Value}"));
    }
    else
    {
        var awsHostedZoneRecordValue = awsHostedZoneRecord.ResourceRecords.FirstOrDefault();
        Console.WriteLine($"{awsHostedZoneRecord.Type,5} | {awsHostedZoneRecord.Name.PadRight(maxNameLength, ' ')} -> {awsHostedZoneRecordValue.Value}");
    }

}

Console.Write("Retrieving external IP...");
var externalIp = await GetExternalIp();
Console.WriteLine($"[{externalIp}] OK!");

while (true)
{
    Console.Write("Sending change request to AWS Route 53...");
    var changes = new List<Change>();
    foreach (var configTargetRecord in config.TargetRecords)
    {
        var recordSet = new ResourceRecordSet(configTargetRecord.Name, configTargetRecord.Type);
        recordSet.ResourceRecords.Add(new ResourceRecord(externalIp));
        recordSet.TTL = 60;
        changes.Add(new Change(ChangeAction.UPSERT, recordSet));
    }

    var awsChangeRequest = new ChangeResourceRecordSetsRequest(awsTargetHostedZone.Id, new ChangeBatch(changes));
    var awsChangeResponse = await awsRoute53Client.ChangeResourceRecordSetsAsync(awsChangeRequest);

    Console.WriteLine($"Sent! Status {awsChangeResponse.ChangeInfo.Status}");
    await Task.Delay(config.Interval * 1000);
}

return;


async Task<string> GetExternalIp()
{
    using var client = new HttpClient();
    return await client.GetStringAsync("https://api.ipify.org/");
}