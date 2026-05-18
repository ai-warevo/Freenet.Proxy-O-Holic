using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace AiWarevo.Freenet.ProxyOHolic;

public class ProxyServer
{
    private readonly ProxySettings _settings;
    private readonly ILogger<ProxyServer> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private long _connectionCounter;

    public ProxyServer(ProxySettings settings, ILoggerFactory loggerFactory)
    {
        _settings = settings;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ProxyServer>();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(_settings.ListenAddress, _settings.Port);

        try
        {
            listener.Start();
            _logger.LogInformation("Freenet.Proxy-O-Holic успешно инициализирован!");
            LogAvailableAddresses();
            _logger.LogInformation("Ожидание подключений от телефона...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Критическая ошибка при запуске сервера на порту {Port}", _settings.Port);
            throw;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await listener.AcceptTcpClientAsync(cancellationToken);
                long connectionId = Interlocked.Increment(ref _connectionCounter);

                // Отдаем обработку отдельному классу в бэкграунд-таску
                var handler = new ClientHandler(clientSocket, connectionId, _settings, _loggerFactory.CreateLogger<ClientHandler>());
                _ = Task.Run(() => handler.ProcessAsync(), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Ошибка при приеме входящего подключения");
            }
        }
    }

    private void LogAvailableAddresses()
    {
        _logger.LogInformation("--> Прокси слушает порт: {Port}", _settings.Port);
        _logger.LogInformation("--> Доступные IP-адреса этого ПК для настройки Wi-Fi на телефоне:");

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            bool hasAddresses = false;

            foreach (var ni in interfaces)
            {
                // Фильтруем только работающие сетевые карты (Wi-Fi и Ethernet), отсекаем виртуальные и loopback
                if (ni.OperationalStatus != OperationalStatus.Up || 
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork) // Только IPv4
                    {
                        _logger.LogInformation("    [{InterfaceName}]: {IPAddress}", ni.Name, addr.Address);
                        hasAddresses = true;
                    }
                }
            }

            if (!hasAddresses)
            {
                _logger.LogWarning("    Активные IPv4-адреса в локальной сети не найдены. Проверьте подключение к роутеру.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось автоматически перечислить сетевые интерфейсы");
            _logger.LogInformation("    [Все интерфейсы]: 0.0.0.0");
        }
    }
}
