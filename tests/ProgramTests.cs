using FluentAssertions;
using Xunit;

namespace AiWarevo.Freenet.ProxyOHolic.Tests;

public class ProgramTests
{
    [Fact]
    public void Main_EntryPoint_ShouldBeValidAndCompiled()
    {
        // Arrange & Act
        var entryPoint = typeof(ProxyServer).Assembly.EntryPoint;

        // Assert
        entryPoint.Should().NotBeNull("Точка входа Program.cs должна успешно компилироваться в Top-Level Statements");
        entryPoint!.Name.Should().Contain("Main");
    }
}
