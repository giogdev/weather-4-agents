using Weather4Agents.Domain.ValueObjects;

namespace Weather4Agents.Test.Domain;

/// <summary>
/// Rules for location names: what the API accepts (<see cref="LocationName.IsValid"/>) and the
/// canonical spelling used for both the cache key and the provider URL
/// (<see cref="LocationName.Normalize"/>).
/// </summary>
public class LocationNameTests
{
    [Theory]
    [InlineData("Bergamo")]
    [InlineData("San Pellegrino Terme")]
    [InlineData("Sant'Angelo Lodigiano")]
    [InlineData("Forlì")]
    [InlineData("san-pellegrino-terme")]
    public void IsValid_AcceptsRealWorldLocationNames(string input)
        => Assert.True(LocationName.IsValid(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("berg4mo")]
    [InlineData("milano!")]
    [InlineData("a/b")]
    [InlineData("rome;drop")]
    [InlineData("---")] // separators only, no letters
    public void IsValid_RejectsDisallowedInput(string input)
        => Assert.False(LocationName.IsValid(input));

    [Fact]
    public void IsValid_BoundsTheLength()
    {
        Assert.True(LocationName.IsValid(new string('a', LocationName.MaxLength)));
        Assert.False(LocationName.IsValid(new string('a', LocationName.MaxLength + 1)));
    }

    [Theory]
    [InlineData("San Pellegrino Terme", "san-pellegrino-terme")]
    [InlineData("san-pellegrino-terme", "san-pellegrino-terme")]
    [InlineData("  Bergamo  ", "bergamo")]
    [InlineData("San  Pellegrino   Terme", "san-pellegrino-terme")]
    [InlineData("Sant'Angelo", "sant'angelo")]
    [InlineData("Forlì", "forlì")]
    public void Normalize_ProducesTheCanonicalSpelling(string input, string expected)
        => Assert.Equal(expected, LocationName.Normalize(input));
}
