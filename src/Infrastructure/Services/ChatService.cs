using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AutonomousResearchAgent.Application.Chat;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ChatService(
    ApplicationDbContext dbContext,
    IEmbeddingService embeddingService,
    IHttpClientFactory httpClientFactory,
    IOptions<OpenRouterOptions> options,
    ILogger<ChatService> logger) : IChatService
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<ChatResult> ChatAsync(string question, int topK, CancellationToken cancellationToken)
    {
        var chunks = await SearchRelevantChunksAsync(question, topK, cancellationToken);

        if (chunks.Count == 0)
        {
            return new ChatResult("I couldn't find any relevant information in the knowledge base to answer your question.", []);
        }

        var prompt = BuildPrompt(question, chunks);
        var systemPrompt = @"You are an expert research assistant. Answer questions based ONLY on the provided context chunks.
If the context doesn't contain enough information to fully answer the question, say so clearly.
Always cite your sources using the format [Title](ChunkIndex) where Title is the paper title and ChunkIndex is the chunk number from the context.
Format your response with proper paragraphs and structure.";

        var answer = await GetChatCompletionAsync(systemPrompt, prompt, cancellationToken);

        return new ChatResult(answer, chunks);
    }

    private async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }

        using var httpClient = httpClientFactory.CreateClient("OpenRouter");
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("OpenRouter returned {StatusCode}. Body: {Body}", response.StatusCode, errorBody);
            throw new ExternalDependencyException($"OpenRouter returned {response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string question,
        int topK,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chunks = await SearchRelevantChunksAsync(question, topK, cancellationToken);

        if (chunks.Count == 0)
        {
            yield return "I couldn't find any relevant information in the knowledge base to answer your question.";
            yield break;
        }

        var prompt = BuildPrompt(question, chunks);
        var systemPrompt = @"You are an expert research assistant. Answer questions based ONLY on the provided context chunks.
If the context doesn't contain enough information to fully answer the question, say so clearly.
Always cite your sources using the format [Title](ChunkIndex) where Title is the paper title and ChunkIndex is the chunk number from the context.
Format your response with proper paragraphs and structure.";

        var stream = StreamOpenRouterCompletionAsync(systemPrompt, prompt, cancellationToken);

        await foreach (var token in stream)
        {
            yield return token;
        }
    }

    private async Task<List<ChunkSearchResult>> SearchRelevantChunksAsync(
        string question,
        int topK,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(question, cancellationToken);

        if (IsPostgres())
        {
            return await SearchChunksWithDatabaseScoringAsync(queryEmbedding, topK, cancellationToken);
        }

        return await SearchChunksInMemoryAsync(queryEmbedding, topK, cancellationToken);
    }

    private async Task<List<ChunkSearchResult>> SearchChunksWithDatabaseScoringAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT pe.""Id"", pe.""DocumentChunkId"", pc.""Text"", p.""Title"", p.""Id"", (pe.""Vector"" <-> @query_embedding) AS ""Distance""
            FROM ""paper_embeddings"" pe
            INNER JOIN ""document_chunks"" pc ON pe.""DocumentChunkId"" = pc.""Id""
            INNER JOIN ""paper_documents"" pd ON pc.""PaperDocumentId"" = pd.""Id""
            INNER JOIN ""papers"" p ON pd.""PaperId"" = p.""Id""
            WHERE pe.""Vector"" IS NOT NULL
              AND pe.""EmbeddingType"" = 'DocumentChunk'
            ORDER BY pe.""Vector"" <-> @query_embedding
            LIMIT @topK";

        var embeddingParam = new NpgsqlParameter("query_embedding", new Vector(queryEmbedding));
        command.Parameters.Add(embeddingParam);
        command.Parameters.Add(new NpgsqlParameter("topK", topK));

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ChunkSearchResult>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ChunkSearchResult(
                PaperId: reader.GetGuid(4),
                PaperTitle: reader.GetString(3),
                ChunkId: reader.GetGuid(1),
                ChunkText: reader.GetString(2),
                Score: 1 - reader.GetDouble(5)));
        }

        return results;
    }

    private async Task<List<ChunkSearchResult>> SearchChunksInMemoryAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Include(e => e.DocumentChunk)
                .ThenInclude(c => c!.PaperDocument)
                    .ThenInclude(d => d.Paper)
            .Where(e =>
                e.Vector != null &&
                e.EmbeddingType == EmbeddingType.DocumentChunk &&
                e.DocumentChunk != null &&
                e.DocumentChunk.PaperDocument != null &&
                e.DocumentChunk.PaperDocument.Paper != null)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(e => new
            {
                Embedding = e,
                Score = VectorMath.CosineSimilarity(e.Vector!, queryEmbedding)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new ChunkSearchResult(
                x.Embedding.DocumentChunk!.PaperDocument.Paper.Id,
                x.Embedding.DocumentChunk.PaperDocument.Paper.Title,
                x.Embedding.DocumentChunk.Id,
                x.Embedding.DocumentChunk.Text,
                x.Score))
            .ToList();
    }

    private static string BuildPrompt(string question, IReadOnlyList<ChunkSearchResult> chunks)
    {
        var contextParts = chunks.Select((c, i) =>
            $"[Context {i + 1}]\nPaper: {c.PaperTitle}\nContent: {c.ChunkText}");
        var context = string.Join("\n\n---\n\n", contextParts);

        return $@"Question: {question}

Context:
{context}

Provide your answer below. Start with a brief summary if the question requires it, then provide the detailed answer citing sources as instructed.";
    }

    private async IAsyncEnumerable<string> StreamOpenRouterCompletionAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            yield return "OpenRouter API key is not configured.";
            yield break;
        }

        using var httpClient = httpClientFactory.CreateClient("OpenRouter");
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                stream = true,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            })
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("OpenRouter streaming returned {StatusCode}. Body: {Body}", response.StatusCode, errorBody);
            yield return $"Error: Received {response.StatusCode} from OpenRouter.";
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                string? textToYield = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("choices", out var choices) &&
                        choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0];
                        if (delta.TryGetProperty("delta", out var deltaObj) &&
                            deltaObj.TryGetProperty("content", out var content))
                        {
                            var text = content.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                textToYield = text;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse SSE message: {Line}", line);
                }

                if (textToYield is not null)
                {
                    yield return textToYield;
                }
            }
        }
    }

    private bool IsPostgres() => string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
}
