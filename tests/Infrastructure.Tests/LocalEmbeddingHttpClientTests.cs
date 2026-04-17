using System.Net;
using System.Net.Http;
using System.Text;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class LocalEmbeddingHttpClientTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_returns_embedding_when_response_vector_length_matches_configured_dimensions()
    {
        var client = CreateClient(
            responseJson: """
            {"embedding":[0.1,0.2,0.3]}
            """,
            vectorDimensions: 3);

        var result = await client.GenerateEmbeddingAsync("test content", CancellationToken.None);

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_throws_when_response_vector_length_does_not_match_configured_dimensions()
    {
        var client = CreateClient(
            responseJson: """
            {"embedding":[0.1,0.2]}
            """,
            vectorDimensions: 3);

        var ex = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            client.GenerateEmbeddingAsync("test content", CancellationToken.None));

        Assert.Contains("expected 3", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("got 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalEmbeddingHttpClient CreateClient(string responseJson, int vectorDimensions)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new StubHttpMessageHandler(response))
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var options = Options.Create(new LocalEmbeddingOptions
        {
            VectorDimensions = vectorDimensions
        });

        return new LocalEmbeddingHttpClient(httpClient, options, NullLogger<LocalEmbeddingHttpClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
