using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ClientHandlerTests
{
    private readonly ProxySettings _settings = new();

    [Fact]
    public async Task ProcessAsync_ShouldHandleInvalidRequest_Gracefully()
    {
        // Arrange: Создаем пару связанных сокетов в памяти (Loopback туннель)
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        
        var clientTask = listener.AcceptTcpClientAsync();
        using var sourceClient = new TcpClient();
        await sourceClient.ConnectAsync(System.Net.IPAddress.Loopback, ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
        using var mockClient = await clientTask;

        // Отправляем битый мусор вместо валидного HTTP-запроса
        using var stream = sourceClient.GetStream();
        var garbageData = Encoding.ASCII.GetBytes("INVALID_DATA_WITHOUT_SPACES\r\n\r\n");
        await stream.WriteAsync(garbageData);

        var handler = new ClientHandler(mockClient, 1, _settings, NullLogger<ClientHandler>.Instance);

        // Act & Assert
        // Метод должен завершиться штатно (return), залогировать варнинг и не упасть по Exception
        Func<Task> act = async () => await handler.ProcessAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_ShouldHandleZeroBytesRead_Gracefully()
    {
        // Arrange
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        
        var clientTask = listener.AcceptTcpClientAsync();
        using var sourceClient = new TcpClient();
        await sourceClient.ConnectAsync(System.Net.IPAddress.Loopback, ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
        using var mockClient = await clientTask;

        // Закрываем клиентский сокет сразу, имитируя мгновенный обрыв соединения до отправки данных
        sourceClient.Close();

        var handler = new ClientHandler(mockClient, 2, _settings, NullLogger<ClientHandler>.Instance);

        // Act & Assert
        Func<Task> act = async () => await handler.ProcessAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_ShouldHandleFailedTargetConnection_Gracefully()
    {
        // Arrange
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        
        var clientTask = listener.AcceptTcpClientAsync();
        using var sourceClient = new TcpClient();
        await sourceClient.ConnectAsync(System.Net.IPAddress.Loopback, ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
        using var mockClient = await clientTask;

        // Отправляем запрос к несуществующему хосту на неиспользуемый порт
        using var stream = sourceClient.GetStream();
        var connectData = Encoding.ASCII.GetBytes("CONNECT 127.0.0.1:9999 HTTP/1.1\r\n\r\n");
        await stream.WriteAsync(connectData);

        var handler = new ClientHandler(mockClient, 3, _settings, NullLogger<ClientHandler>.Instance);

        // Act & Assert
        // Метод поймает ошибку подключения к таргету, залогирует её через `LogError` и корректно выйдет
        Func<Task> act = async () => await handler.ProcessAsync();
        await act.Should().NotThrowAsync();
    }
}
