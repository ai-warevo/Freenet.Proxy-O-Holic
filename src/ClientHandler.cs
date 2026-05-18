using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AiWarevo.Freenet.ProxyOHolic;

public class ClientHandler
{
    private readonly TcpClient _client;
    private readonly long _id;
    private readonly ProxySettings _settings;
    private readonly ILogger<ClientHandler> _logger;
    private readonly string _remoteEndPoint;

    public ClientHandler(TcpClient client, long id, ProxySettings settings, ILogger<ClientHandler> logger)
    {
        _client = client;
        _id = id;
        _settings = settings;
        _logger = logger;
        _remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
    }

    public async Task ProcessAsync()
    {
        _logger.LogDebug("[#{Id}] Новое подключение от: {EndPoint}", _id, _remoteEndPoint);

        using (_client)
        await using (var clientStream = _client.GetStream())
        {
            try
            {
                var buffer = new byte[_settings.BufferSize];
                int bytesRead = await clientStream.ReadAsync(buffer);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("[#{Id}] Устройство закрыло соединение без отправки данных", _id);
                    return;
                }

                if (!TryParseRequest(buffer, bytesRead, out var method, out var targetHost, out var targetPort, out var firstLine))
                {
                    _logger.LogWarning("[#{Id}] Некорректный HTTP-запрос: {FirstLine}", _id, firstLine);
                    return;
                }

                _logger.LogInformation("[#{Id}] Запрос: {Method} -> {Host}:{Port}", _id, method, targetHost, targetPort);
                _logger.LogDebug("[#{Id}] Установка соединения с {Host}:{Port}...", _id, targetHost, targetPort);

                using var targetClient = new TcpClient();
                try
                {
                    await targetClient.ConnectAsync(targetHost, targetPort);
                }
                catch (Exception ex)
                {
                    _logger.LogError("[#{Id}] Ошибка подключения к серверу назначения {Host}: {Message}", _id, targetHost, ex.Message);
                    return;
                }

                await using var targetStream = targetClient.GetStream();
                _logger.LogDebug("[#{Id}] Соединение с {Host} установлено", _id, targetHost);

                if (method == "CONNECT")
                {
                    var response = "HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray();
                    await clientStream.WriteAsync(response);
                    _logger.LogDebug("[#{Id}] Туннель CONNECT подтвержден для устройства", _id);
                }
                else
                {
                    await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                }

                _logger.LogDebug("[#{Id}] Запуск двустороннего обмена данными...", _id);
                var clientToTarget = clientStream.CopyToAsync(targetStream);
                var targetToClient = targetStream.CopyToAsync(clientStream);

                await Task.WhenAny(clientToTarget, targetToClient);
                _logger.LogDebug("[#{Id}] Соединение закрыто штатно", _id);
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning("[#{Id}] Сетевой обрыв (клиент отключился): {Message}", _id, ioEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[#{Id}] Критическая ошибка при обработке трафика", _id);
            }
        }
    }

    private static bool TryParseRequest(byte[] buffer, int bytesRead, out string method, out string host, out int port, out string firstLine)
    {
        method = string.Empty;
        host = string.Empty;
        port = 80;
        
        var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        firstLine = request.Split("\r\n").FirstOrDefault() ?? string.Empty;
        var parts = firstLine.Split(' ');

        if (parts.Length < 2) return false;

        method = parts[0].ToUpper();
        string rawTarget = parts[1];

        if (method == "CONNECT")
        {
            var hostParts = rawTarget.Split(':');
            host = hostParts[0];
            port = hostParts.Length > 1 ? int.Parse(hostParts[1]) : 443;
        }
        else
        {
            var uri = new Uri(rawTarget);
            host = uri.Host;
            port = uri.Port;
        }

        return true;
    }
}
