namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class RecursiveCharacterTextChunkingService : ITextChunkingService
{
    private static readonly char[] DefaultSeparators = ['\n', '\r', ' ', '\t'];

    public IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 64)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<TextChunk>();
        var separators = DefaultSeparators;
        var effectiveOverlap = Math.Min(overlap, chunkSize / 2);
        if (effectiveOverlap >= chunkSize) effectiveOverlap = chunkSize / 2;

        var startIndex = 0;
        var chunkIndex = 0;

        while (startIndex < text.Length)
        {
            var endIndex = Math.Min(startIndex + chunkSize, text.Length);
            var chunkText = text[startIndex..endIndex];

            if (endIndex < text.Length)
            {
                var lastSeparatorIndex = -1;
                for (var i = chunkText.Length - 1; i >= 0; i--)
                {
                    if (separators.Contains(chunkText[i]))
                    {
                        lastSeparatorIndex = i;
                        break;
                    }
                }

                if (lastSeparatorIndex > chunkSize / 4)
                {
                    chunkText = chunkText[..lastSeparatorIndex].TrimEnd();
                    endIndex = startIndex + lastSeparatorIndex;
                }
            }

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(new TextChunk
                {
                    Text = chunkText.Trim(),
                    Index = chunkIndex,
                    StartPosition = startIndex,
                    EndPosition = endIndex
                });
            }

            chunkIndex++;

            if (endIndex >= text.Length)
            {
                break;
            }

            startIndex = endIndex - effectiveOverlap;
        }

        return chunks;
    }
}
