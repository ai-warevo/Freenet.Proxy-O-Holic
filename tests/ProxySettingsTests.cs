using System.Net;
using FluentAssertions;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ProxySettingsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var settings = new ProxySettings();

        // Assert
        settings.Port.Should().Be(8118);
        settings.BufferSize.Should().Be(8192);
        settings.ListenAddress.Should().Be(IPAddress.Any);
    }

    [Theory]
    [InlineData(8080, 4096)]
    [InlineData(9000, 16384)]
    public void Constructor_ShouldApplyCustomValues(int customPort, int customBufferSize)
    {
        // Arrange & Act
        var settings = new ProxySettings(Port: customPort, BufferSize: customBufferSize);

        // Assert
        settings.Port.Should().Be(customPort);
        settings.BufferSize.Should().Be(customBufferSize);
        settings.ListenAddress.Should().Be(IPAddress.Any);
    }
}
