namespace AutonomousResearchAgent.Application.Common;

public sealed class AuthenticationException : ApplicationExceptionBase
{
    public AuthenticationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}