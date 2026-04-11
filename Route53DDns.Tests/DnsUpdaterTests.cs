using Amazon.Route53.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Route53DDns;
using Xunit;

namespace Route53DDns.Tests;

public class DnsUpdaterTests
{
    private readonly IExternalIpService _ipService = Substitute.For<IExternalIpService>();
    private readonly IRoute53Service _route53Service = Substitute.For<IRoute53Service>();
    private readonly Config _config;
    private readonly DnsUpdater _updater;

    public DnsUpdaterTests()
    {
        _config = new Config(
            "access",
            "secret",
            "example.com",
            [new TargetRecordConfig("A", "test.example.com")],
            1 // 1 second interval for tests
        );
        _updater = new DnsUpdater(_ipService, _route53Service, _config, NullLogger<DnsUpdater>.Instance);
    }

    [Fact]
    public async Task RunAsync_HostedZoneNotFound_ReturnsEarly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _route53Service.FindHostedZoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((HostedZone?)null);

        // Act
        await _updater.RunAsync(cts.Token);

        // Assert
        await _route53Service.DidNotReceive().ListRecordsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_IpChanged_UpdatesRecords()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var hostedZone = new HostedZone { Id = "Z123", Name = "example.com." };
        var initialIp = "1.2.3.4";

        _route53Service.FindHostedZoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hostedZone);
        _route53Service.ListRecordsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<ResourceRecordSet>());

        // Mock getting the IP twice: first time a new IP, then cancel
        _ipService.GetExternalIpAsync(Arg.Any<CancellationToken>())
            .Returns(
                x => initialIp,
                x => {
                    cts.Cancel();
                    return (string?)null;
                });

        // Act
        try
        {
            await _updater.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Assert
        await _route53Service.Received(1).UpdateRecordsAsync(
            hostedZone.Id,
            initialIp,
            _config.TargetRecords!,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RunAsync_IpSame_DoesNotUpdateRecords()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var hostedZone = new HostedZone { Id = "Z123", Name = "example.com." };
        var ip = "1.2.3.4";

        _route53Service.FindHostedZoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hostedZone);
        _route53Service.ListRecordsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<ResourceRecordSet>());

        // Get same IP twice, then cancel
        _ipService.GetExternalIpAsync(Arg.Any<CancellationToken>())
            .Returns(
                x => ip,
                x => ip,
                x => {
                    cts.Cancel();
                    return (string?)null;
                });

        // Act
        try
        {
            await _updater.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Assert
        // Should only be called once for the first detection (if we consider _lastExternalIp is initially null)
        // Actually, in DnsUpdater.cs: if (externalIp != _lastExternalIp) { ... _lastExternalIp = externalIp; }
        // First call: initialIp ("1.2.3.4") != null -> UpdateRecordsAsync called, _lastExternalIp = "1.2.3.4"
        // Second call: "1.2.3.4" != "1.2.3.4" is false -> UpdateRecordsAsync NOT called.
        await _route53Service.Received(1).UpdateRecordsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TargetRecordConfig[]>(), Arg.Any<CancellationToken>());
    }
}
