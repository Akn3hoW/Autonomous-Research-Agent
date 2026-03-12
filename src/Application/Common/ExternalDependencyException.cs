namespace AutonomousResearchAgent.Application.Common;

public sealed class ExternalDependencyException : ApplicationExceptionBase
{
    public ExternalDependencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
