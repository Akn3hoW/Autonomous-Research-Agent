namespace AutonomousResearchAgent.Application.Common;

public sealed class NotFoundException : ApplicationExceptionBase
{
    public NotFoundException(string resourceName, Guid id)
        : base($"{resourceName} '{id}' was not found.")
    {
    }
}

