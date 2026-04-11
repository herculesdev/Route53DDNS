namespace Route53DDns.Configuration;

public class AwsConfig
{
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? TargetHostedZone { get; set; }
    public TargetRecordConfig[]? TargetRecords { get; set; }
    public int Interval { get; set; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetHostedZone)) throw new ArgumentException("TargetHostedZone is required.");
        if (TargetRecords == null || TargetRecords.Length == 0)
            throw new ArgumentException("At least one TargetRecord must be configured.");
        foreach (var record in TargetRecords!)
        {
            if (string.IsNullOrWhiteSpace(record.Name) || string.IsNullOrWhiteSpace(record.Type))
                throw new ArgumentException("Record Name and Type are required.");
            var type = record.Type.ToUpperInvariant();
            if (type != "A" && type != "AAAA")
                throw new ArgumentException("Record type not supported (A and AAAA only).");
        }

        if (Interval <= 0) throw new ArgumentException("Interval must be a positive value in seconds.");
    }
}