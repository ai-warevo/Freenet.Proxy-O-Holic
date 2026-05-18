using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ProxyServerTests
{
    [Fact]
    public async Task StartAsync_ShouldStartAndStop_WhenCancellationRequested()
    {
        // Arrange: Запускаем на случайном свободном порту (0), чтобы не занять рабочий порт 8118
        var settings = new ProxySettings(Port: 0); 
        var server = new ProxyServer(settings, NullLoggerFactory.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        // Запускаем сервер
        var serverTask = server.StartAsync(cts.Token);

        // Имитируем быструю отмену (например, остановка приложения пользователем)
        await cts.CancelAsync();

        // Assert
        // Метод должен завершиться корректно без выбрасывания необработанных Exception
        Func<Task> act = async () => await serverTask;
        await act.Should().NotThrowAsync();
        serverTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldThrow_WhenPortIsAlreadyOccupied()
    {
        // Arrange: Создаем первый сервер и занимаем случайный свободный порт
        var settings = new ProxySettings(Port: 0);
        var server1 = new ProxyServer(settings, NullLoggerFactory.Instance);
        using var cts1 = new CancellationTokenSource();
        
        var server1Task = server1.StartAsync(cts1.Token);
        
        // Немного ждем, чтобы TcpListener успел захватить порт, и считываем, какой порт выделила ОС
        await Task.Delay(100); 

        // Имитируем запуск второго сервера на том же самом порту
        // Так как реальный выделенный порт мы не знаем, для теста на занятый порт 
        // надежнее попытаться переиспользовать фиксированный системный порт, который часто занят, 
        // либо запустить его повторно с теми же настройками (если ОС зафиксирует порт для этого процесса)
        // Но самый гарантированный способ вызвать SocketException — занять конкретный порт через TcpListener
        using var дубликатЛистенер = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 19999);
        дубликатЛистенер.Start();

        var settingsConflict = new ProxySettings(Port: 19999);
        var server2 = new ProxyServer(settingsConflict, NullLoggerFactory.Instance);

        // Act & Assert
        // Второй сервер должен упасть с критической ошибкой, так как порт занят
        Func<Task> act = async () => await server2.StartAsync(default);
        await act.Should().ThrowAsync<System.Net.Sockets.SocketException>();

        // Очистка ресурсов
        дубликатЛистенер.Stop();
        await cts1.CancelAsync();
        await server1Task;
    }
}
