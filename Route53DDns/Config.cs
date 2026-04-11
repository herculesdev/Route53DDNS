namespace Route53DDns;

public record Config(
    string? AccessKey,
    string? SecretKey,
    string? TargetHostedZone,
    TargetRecordConfig[]? TargetRecords,
    int Interval
)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetHostedZone))
            throw new ArgumentException("TargetHostedZone é obrigatório.");

        if (TargetRecords == null || TargetRecords.Length == 0)
            throw new ArgumentException("Pelo menos um TargetRecord deve ser configurado.");

        foreach (var record in TargetRecords!)
        {
            if (string.IsNullOrWhiteSpace(record.Name) || string.IsNullOrWhiteSpace(record.Type))
                throw new ArgumentException("Nome e Tipo do registro são obrigatórios.");

            var type = record.Type.ToUpperInvariant();
            if (type != "A" && type != "AAAA")
                throw new ArgumentException($"Tipo de registro '{record.Type}' não suportado (apenas A e AAAA).");
        }

        if (Interval <= 0)
            throw new ArgumentException("O Intervalo deve ser um valor positivo em segundos.");
    }
}

public record TargetRecordConfig(string Type, string Name);