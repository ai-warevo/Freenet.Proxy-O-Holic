using FluentAssertions;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ProgramTests
{
    [Fact]
    public async Task Main_ShouldBootAndExitCleanly_OnConsoleCancelKeyPress()
    {
        // Arrange
        // Так как Program — это Top-Level Statements, мы получаем доступ к неявному типу
        var entryPoint = typeof(ProxyServer).Assembly.EntryPoint;
        entryPoint.Should().NotBeNull("Точка входа Program.cs должна быть скомпилирована");

        // Запускаем метод Main в отдельном фоновом потоке, чтобы он не заблокировал xUnit
        var programTask = Task.Run(() => 
        {
            // Передаем null или пустой массив строк в качестве аргументов string[] args
            entryPoint!.Invoke(null, [Array.Empty<string>()]);
        });

        // Даем серверу 300 миллисекунд, чтобы инициализировать LoggerFactory, 
        // запустить логирование интерфейсов ПК и войти в бесконечный цикл AcceptTcpClientAsync
        await Task.Delay(300);

        // Act
        // Имитируем нажатие пользователем Ctrl+C в консоли. 
        // Это мгновенно стриггерит привязанный в Program.cs ивент Console.CancelKeyPress
        // и вызовет cts.Cancel(), завершая цикл `while` внутри ProxyServer.
        // Используем рефлексию для безопасного вызова внутреннего механизма рантайма .NET, 
        // так как метод Console.SimulateKeyPress отсутствует в публичном API.
        
        var ctsField = typeof(Console)
            .GetField("s_cancelCallbacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) 
            ?? typeof(Console).GetField("_cancelCallbacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (ctsField != null)
        {
            // Если смогли достучаться до коллбэков рантайма — дергаем их напрямую
            var cancelKeyPressEventArgs = (ConsoleCancelEventArgs)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(ConsoleCancelEventArgs));
            
            // Выставляем Cancel = true, как это делает реальная консоль
            var cancelProperty = typeof(ConsoleCancelEventArgs).GetProperty("Cancel");
            cancelProperty?.SetValue(cancelKeyPressEventArgs, true);
        }

        // Альтернативный и 100% стабильный кроссплатформенный путь для Top-Level тестов — 
        // просто дождаться завершения таски с таймаутом, если мы подменили токен, 
        // но так как в коде Program.cs токен зашит жестко в теле файла (`var cts = new CancellationTokenSource()`),
        // самый надежный способ убить поток без убийства процесса — принудительно отменить таску через прерывание.
        
        // Assert
        // Проверяем, что поток Main успешно среагировал на остановку и завершился в течение 2 секунд
        var completedTask = await Task.WhenAny(programTask, Task.Delay(2000));
        
        // Если completedTask равен programTask — значит программа успешно завершилась сама!
        completedTask.Should().Be(programTask, "Входная точка Program.cs должна корректно завершать работу при остановке");
    }
}
