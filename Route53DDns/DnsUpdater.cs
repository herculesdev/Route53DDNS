using Microsoft.Extensions.Logging;

namespace Route53DDns;

public class DnsUpdater(
    IExternalIpService ipService,
    IRoute53Service route53Service,
    Config config,
    ILogger<DnsUpdater> logger
)
{
    private string? _lastExternalIp;

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Iniciando DnsUpdater...");
        config.Validate();

        var hostedZone = await route53Service.FindHostedZoneAsync(config.TargetHostedZone!, ct);
        if (hostedZone == null)
        {
            logger.LogCritical("Zona hospedada alvo '{Target}' não encontrada no Route 53.", config.TargetHostedZone);
            return;
        }

        logger.LogInformation("Zona hospedada encontrada: {Name} (ID: {Id})", hostedZone.Name, hostedZone.Id);

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
            logger.LogInformation("Nenhum registro encontrado na zona.");
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var externalIp = await ipService.GetExternalIpAsync(ct);
                if (externalIp == null)
                {
                    logger.LogWarning("Não foi possível obter o IP externo. Tentando novamente em {Interval}s.",
                        config.Interval);
                }
                else if (externalIp != _lastExternalIp)
                {
                    logger.LogInformation("Mudança de IP detectada: [{OldIp}] -> [{NewIp}]", _lastExternalIp ?? "N/A",
                        externalIp);
                    await route53Service.UpdateRecordsAsync(hostedZone.Id, externalIp, config.TargetRecords!, ct);
                    _lastExternalIp = externalIp;
                    logger.LogInformation("Registros atualizados com sucesso.");
                }
                else
                {
                    logger.LogInformation("IP externo não mudou: [{Ip}]", externalIp);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado no loop de atualização.");
            }

            try
            {
                await Task.Delay(config.Interval * 1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("DnsUpdater encerrado.");
    }
}