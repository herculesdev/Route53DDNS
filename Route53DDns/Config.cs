namespace Route53DDns;

public record Config(
    string AccessKey,
    string SecretKey,
    string TargetHostedZone,
    TargetRecordConfig[] TargetRecords,
    int Interval
    );

public record TargetRecordConfig(string Type, string Name);