using Route53DDns;
using Xunit;

namespace Route53DDns.Tests;

public class ConfigTests
{
    [Fact]
    public void Validate_ValidConfig_ShouldNotThrow()
    {
        // Arrange
        var config = new Config(
            "access",
            "secret",
            "example.com",
            [new TargetRecordConfig("A", "test.example.com")],
            60
        );

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
        var config = new Config("a", "s", zone, [new TargetRecordConfig("A", "n")], 60);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("TargetHostedZone é obrigatório", ex.Message);
    }

    [Fact]
    public void Validate_EmptyTargetRecords_ShouldThrow()
    {
        // Arrange
        var config = new Config("a", "s", "z", [], 60);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Pelo menos um TargetRecord deve ser configurado", ex.Message);
    }

    [Theory]
    [InlineData(null, "A")]
    [InlineData("", "A")]
    [InlineData("n", null)]
    [InlineData("n", "")]
    public void Validate_InvalidRecord_ShouldThrow(string name, string type)
    {
        // Arrange
        var config = new Config("a", "s", "z", [new TargetRecordConfig(type!, name!)], 60);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Nome e Tipo do registro são obrigatórios", ex.Message);
    }

    [Fact]
    public void Validate_UnsupportedRecordType_ShouldThrow()
    {
        // Arrange
        var config = new Config("a", "s", "z", [new TargetRecordConfig("CNAME", "n")], 60);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("não suportado", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidInterval_ShouldThrow(int interval)
    {
        // Arrange
        var config = new Config("a", "s", "z", [new TargetRecordConfig("A", "n")], interval);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("O Intervalo deve ser um valor positivo", ex.Message);
    }
}
