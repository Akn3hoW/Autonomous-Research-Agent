namespace AutonomousResearchAgent.Api.Contracts.Common;

internal static class ValidatorHelpers
{
    public static bool BeValidEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out _);
}
