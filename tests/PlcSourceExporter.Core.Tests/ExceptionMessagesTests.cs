using System.Reflection;
using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ExceptionMessagesTests
{
    [Fact]
    public void UnwrapsTargetInvocationException()
    {
        var exception = new TargetInvocationException(
            new InvalidOperationException("Programming language 'ProDiag' is not supported during import/export."));

        var message = ExceptionMessages.GetMeaningfulMessage(exception);

        Assert.Equal("Programming language 'ProDiag' is not supported during import/export.", message);
    }
}
