using System.Text.RegularExpressions;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed partial class LocalDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly Regex GarbledMathPattern = GenerateGarbledMathPattern();

    public async Task<string?> ExtractAsync(byte[] bytes, string? mediaType, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true ||
            extension is ".txt" or ".md" or ".json" or ".csv")
        {
            return await Task.FromResult<string?>(System.Text.Encoding.UTF8.GetString(bytes));
        }

        if (extension == ".pdf" || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = new MemoryStream(bytes);
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(stream);
            var text = string.Join("\n\n", pdf.GetPages().Select(page => page.Text).Where(pageText => !string.IsNullOrWhiteSpace(pageText))).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (IsGarbledMath(text))
            {
                return null;
            }

            return text;
        }

        return await Task.FromResult<string?>(null);
    }

    public static bool IsGarbledMath(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            return false;
        }

        int mathIndicators = 0;
        mathIndicators += GarbledMathPattern.Matches(text).Count;

        if (mathIndicators > text.Length / 100)
        {
            return true;
        }

        double letterRatio = text.Count(char.IsLetter) / (double)text.Length;
        if (letterRatio < 0.3)
        {
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"[\u2200-\u22FF\u27BF\u2B50\u2600-\u26FF]", RegexOptions.Compiled)]
    private static partial Regex GenerateGarbledMathPattern();
}