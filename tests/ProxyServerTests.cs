using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ProxyServerTests
{
    [Fact]
    public async Task StartAsync_ShouldStartAndStop_WhenCancellationRequested()
    {
        // Arrange
        var settings = new ProxySettings(Port: 0); 
        var server = new ProxyServer(settings, NullLoggerFactory.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        var serverTask = server.StartAsync(cts.Token);
        await cts.CancelAsync();

        // Assert
        Func<Task> act = async () => await serverTask;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenPortIsAlreadyOccupied()
    {
        // Arrange: Жестко занимаем конкретный порт в системе
        using var duplicateListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 28118);
        duplicateListener.Start();

        var settingsConflict = new ProxySettings(Port: 28118);
        var server = new ProxyServer(settingsConflict, NullLoggerFactory.Instance);

        // Act & Assert
        Func<Task> act = async () => await server.StartAsync(default);
        await act.Should().ThrowAsync<System.Net.Sockets.SocketException>();

        // Очистка
        duplicateListener.Stop();
    }
}
