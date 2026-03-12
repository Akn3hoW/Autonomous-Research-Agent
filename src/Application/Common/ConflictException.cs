namespace AutonomousResearchAgent.Application.Common;

public sealed class ConflictException : ApplicationExceptionBase
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

