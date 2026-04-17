using AutonomousResearchAgent.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Tests;

public sealed class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_returns_1_for_identical_vectors()
    {
        var vector = new float[] { 1f, 2f, 3f };

        var result = VectorMath.CosineSimilarity(vector, vector);

        Assert.Equal(1.0, result, 5);
    }

    [Fact]
    public void CosineSimilarity_returns_0_for_orthogonal_vectors()
    {
        var left = new float[] { 1f, 0f, 0f };
        var right = new float[] { 0f, 1f, 0f };

        var result = VectorMath.CosineSimilarity(left, right);

        Assert.Equal(0.0, result, 5);
    }

    [Fact]
    public void CosineSimilarity_returns_negative_for_opposite_vectors()
    {
        var left = new float[] { 1f, 0f, 0f };
        var right = new float[] { -1f, 0f, 0f };

        var result = VectorMath.CosineSimilarity(left, right);

        Assert.Equal(-1.0, result, 5);
    }

    [Fact]
    public void CosineSimilarity_returns_0_for_empty_vectors()
    {
        var empty = Array.Empty<float>();

        var result = VectorMath.CosineSimilarity(empty, empty);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CosineSimilarity_returns_0_for_mismatched_lengths()
    {
        var left = new float[] { 1f, 2f };
        var right = new float[] { 1f, 2f, 3f };

        var result = VectorMath.CosineSimilarity(left, right);

        Assert.Equal(0.0, result);
    }
}
