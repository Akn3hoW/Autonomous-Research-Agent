namespace AutonomousResearchAgent.Application.Common;

public sealed class InvalidStateException : ApplicationExceptionBase
{
    public InvalidStateException(string message)
        : base(message)
    {
    }
}

