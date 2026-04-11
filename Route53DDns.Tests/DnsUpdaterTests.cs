using Amazon.Route53.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Route53DDns;
using Xunit;
using System.Reflection;
using Route53DDns.Application;
using Route53DDns.Configuration;
using Route53DDns.Services;

namespace Route53DDns.Tests;

public class DnsUpdaterTests
{
    private readonly IExternalIpService _ipService = Substitute.For<IExternalIpService>();
    private readonly IRoute53Service _route53Service = Substitute.For<IRoute53Service>();
    private readonly AwsConfig _awsConfig;
    private readonly DnsUpdater _updater;

    public DnsUpdaterTests()
    {
        _awsConfig = new AwsConfig
        {
            AccessKey = "access",
            SecretKey = "secret",
            TargetHostedZone = "example.com",
            TargetRecords = [new TargetRecordConfig { Type = "A", Name = "test.example.com" }],
            Interval = 1
        };
        var options = Options.Create(_awsConfig);
        _updater = new DnsUpdater(_ipService, _route53Service, options, NullLogger<DnsUpdater>.Instance);
    }

    private Task InvokeExecuteAsync(CancellationToken ct)
    {
        var method = typeof(DnsUpdater).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task)method!.Invoke(_updater, [ct])!;
    }

    [Fact]
    public async Task RunAsync_HostedZoneNotFound_ReturnsEarly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _route53Service.FindHostedZoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((HostedZone?)null);

        // Act
        await InvokeExecuteAsync(cts.Token);

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

        _ipService.GetExternalIpAsync(Arg.Any<CancellationToken>())
            .Returns(
                x => initialIp,
                x => {
                    cts.Cancel();
                    return (string?)null;
                });

        // Act
        try { await InvokeExecuteAsync(cts.Token); } catch (OperationCanceledException) { }

        // Assert
        await _route53Service.Received(1).UpdateRecordsAsync(
            hostedZone.Id,
            initialIp,
            _awsConfig.TargetRecords!,
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

        _ipService.GetExternalIpAsync(Arg.Any<CancellationToken>())
            .Returns(
                x => ip,
                x => ip,
                x => {
                    cts.Cancel();
                    return (string?)null;
                });

        // Act
        try { await InvokeExecuteAsync(cts.Token); } catch (OperationCanceledException) { }

        // Assert
        await _route53Service.Received(1).UpdateRecordsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TargetRecordConfig[]>(), Arg.Any<CancellationToken>());
    }
}
