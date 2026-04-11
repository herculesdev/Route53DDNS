using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Route53DDns.Services;

public interface IExternalIpService
{
    Task<string?> GetExternalIpAsync(CancellationToken ct);
}

public class ExternalIpService(HttpClient httpClient, ILogger<ExternalIpService> logger) : IExternalIpService
{
    private static readonly Regex IpRegex = new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    public async Task<string?> GetExternalIpAsync(CancellationToken ct)
    {
        try
        {
            var response = await httpClient.GetStringAsync("https://api.ipify.org/", ct);
            var ip = response.Trim();
            if (!IpRegex.IsMatch(ip))
            {
                logger.LogWarning("IP returned by service is invalid: {Ip}", ip);
                return null;
            }

            return ip;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting external IP.");
            return null;
        }
    }
}