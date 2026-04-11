using Route53DDns;
using Route53DDns.Configuration;
using Xunit;

namespace Route53DDns.Tests;

public class AwsConfigTests
{
    [Fact]
    public void Validate_ValidConfig_ShouldNotThrow()
    {
        // Arrange
        var config = new AwsConfig
        {
            AccessKey = "access",
            SecretKey = "secret",
            TargetHostedZone = "example.com",
            TargetRecords = [new TargetRecordConfig { Type = "A", Name = "test.example.com" }],
            Interval = 60
        };

        // Act & Assert
        config.Validate();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_MissingTargetHostedZone_ShouldThrow(string zone)
    {
        // Arrange
        var config = new AwsConfig { AccessKey = "a", SecretKey = "s", TargetHostedZone = zone, TargetRecords = [new TargetRecordConfig { Type = "A", Name = "n" }], Interval = 60 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("TargetHostedZone is required.", ex.Message);
    }

    [Fact]
    public void Validate_EmptyTargetRecords_ShouldThrow()
    {
        // Arrange
        var config = new AwsConfig { AccessKey = "a", SecretKey = "s", TargetHostedZone = "z", TargetRecords = [], Interval = 60 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("At least one TargetRecord must be configured.", ex.Message);
    }

    [Theory]
    [InlineData(null, "A")]
    [InlineData("", "A")]
    [InlineData("n", null)]
    [InlineData("n", "")]
    public void Validate_InvalidRecord_ShouldThrow(string name, string type)
    {
        // Arrange
        var config = new AwsConfig { AccessKey = "a", SecretKey = "s", TargetHostedZone = "z", TargetRecords = [new TargetRecordConfig { Type = type!, Name = name! }], Interval = 60 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Record Name and Type are required.", ex.Message);
    }

    [Fact]
    public void Validate_UnsupportedRecordType_ShouldThrow()
    {
        // Arrange
        var config = new AwsConfig { AccessKey = "a", SecretKey = "s", TargetHostedZone = "z", TargetRecords = [new TargetRecordConfig { Type = "CNAME", Name = "n" }], Interval = 60 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Record type not supported", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidInterval_ShouldThrow(int interval)
    {
        // Arrange
        var config = new AwsConfig { AccessKey = "a", SecretKey = "s", TargetHostedZone = "z", TargetRecords = [new TargetRecordConfig { Type = "A", Name = "n" }], Interval = interval };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Interval must be a positive value", ex.Message);
    }
}

