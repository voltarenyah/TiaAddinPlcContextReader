using System.Reflection;

namespace PlcSourceExporter.Core;

public static class ExceptionMessages
{
    public static string GetMeaningfulMessage(Exception exception)
    {
        var current = exception;
        while (current is TargetInvocationException && current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
