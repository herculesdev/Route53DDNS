using Amazon.Route53;
using Amazon.Route53.Model;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Route53DDns;
using Route53DDns.Configuration;
using Route53DDns.Services;
using Xunit;

namespace Route53DDns.Tests;

public class Route53ServiceTests
{
    private readonly IAmazonRoute53 _route53Client = Substitute.For<IAmazonRoute53>();
    private readonly Route53Service _service;

    public Route53ServiceTests()
    {
        _service = new Route53Service(_route53Client, NullLogger<Route53Service>.Instance);
    }

    [Fact]
    public async Task FindHostedZoneAsync_ZoneExists_ReturnsZone()
    {
        // Arrange
        var zoneName = "example.com";
        var expectedName = "example.com.";
        _route53Client.ListHostedZonesAsync(Arg.Any<ListHostedZonesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListHostedZonesResponse
            {
                HostedZones = [new HostedZone { Name = expectedName, Id = "Z123" }]
            });

        // Act
        var result = await _service.FindHostedZoneAsync(zoneName, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedName, result.Name);
    }

    [Fact]
    public async Task FindHostedZoneAsync_ZoneDoesNotExist_ReturnsNull()
    {
        // Arrange
        _route53Client.ListHostedZonesAsync(Arg.Any<ListHostedZonesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListHostedZonesResponse
            {
                HostedZones = [new HostedZone { Name = "other.com.", Id = "Z456" }]
            });

        // Act
        var result = await _service.FindHostedZoneAsync("example.com", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListRecordsAsync_ReturnsRecords()
    {
        // Arrange
        var hostedZoneId = "Z123";
        _route53Client.ListResourceRecordSetsAsync(Arg.Any<ListResourceRecordSetsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListResourceRecordSetsResponse
            {
                ResourceRecordSets = [new ResourceRecordSet { Name = "test.example.com.", Type = RRType.A }]
            });

        // Act
        var result = await _service.ListRecordsAsync(hostedZoneId, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("test.example.com.", result[0].Name);
    }

    [Fact]
    public async Task UpdateRecordsAsync_CallsChangeResourceRecordSetsAsync()
    {
        // Arrange
        var hostedZoneId = "Z123";
        var ip = "1.2.3.4";
        var targets = new[] { new TargetRecordConfig { Type = "A", Name = "test.example.com" } };

        // Act
        await _service.UpdateRecordsAsync(hostedZoneId, ip, targets, CancellationToken.None);

        // Assert
        await _route53Client.Received(1).ChangeResourceRecordSetsAsync(
            Arg.Is<ChangeResourceRecordSetsRequest>(r =>
                r.HostedZoneId == hostedZoneId &&
                r.ChangeBatch.Changes.Count == 1 &&
                r.ChangeBatch.Changes[0].ResourceRecordSet.Name == "test.example.com" &&
                r.ChangeBatch.Changes[0].ResourceRecordSet.ResourceRecords[0].Value == ip
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
