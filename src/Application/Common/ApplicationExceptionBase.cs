namespace AutonomousResearchAgent.Application.Common;

public abstract class ApplicationExceptionBase : Exception
{
    protected ApplicationExceptionBase(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
