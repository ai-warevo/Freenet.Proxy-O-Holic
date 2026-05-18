using Microsoft.Extensions.Logging;
using AiWarevo.Freenet.ProxyOHolic;

// Инициализация логгера
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug); // Debug для детальных логов по каждому [#Id]
});

var settings = new ProxySettings(Port: 8118);
var server = new ProxyServer(settings, loggerFactory);

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await server.StartAsync(cts.Token);
