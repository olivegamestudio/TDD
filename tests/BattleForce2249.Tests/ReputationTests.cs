namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="Reputation"/>: what the galaxy makes of the character, group by group.
/// </summary>
public sealed class ReputationTests
{
    const string Miners = "miners";
    const string Syndicate = "syndicate";

    [Fact]
    public void AGroupTheCharacterHasNeverDealtWith_IsNeutral()
    {
        // answering "neutral" rather than nothing keeps every caller from having to know which
        // groups exist in order to ask about one
        Reputation reputation = new();

        Assert.Equal(0, reputation.With(Miners));
    }

    [Fact]
    public void AGroupTheCharacterHasNeverDealtWith_IsNotListed()
    {
        Reputation reputation = new();

        Assert.Empty(reputation.Standings);
    }

    [Fact]
    public void Adjust_MovesOneGroupsStanding()
    {
        Reputation reputation = new();

        Assert.Equal(10, reputation.Adjust(Miners, 10));

        Assert.Equal(10, reputation.With(Miners));
    }

    [Fact]
    public void Adjust_AccumulatesRatherThanReplacing()
    {
        Reputation reputation = new();

        reputation.Adjust(Miners, 10);
        reputation.Adjust(Miners, -25);

        Assert.Equal(-15, reputation.With(Miners));
    }

    [Fact]
    public void OneGroupsStanding_LeavesEveryOtherGroupWhereItWas()
    {
        // the whole point of holding one standing per group: earning favour with one side of a
        // conspiracy is not the same event as losing it with another
        Reputation reputation = new();

        reputation.Adjust(Miners, 10);

        Assert.Equal(0, reputation.With(Syndicate));
        Assert.Equal([Miners], reputation.Standings.Keys);
    }

    [Fact]
    public void AGroupBackAtZero_IsStillAGroupTheCharacterHasDealtWith()
    {
        // history, not just the current number. A group they fell out with and made it up to is
        // not the same as one they have never met, even though both read zero.
        Reputation reputation = new();
        reputation.Adjust(Miners, 10);

        reputation.Adjust(Miners, -10);

        Assert.Equal(0, reputation.With(Miners));
        Assert.Equal([Miners], reputation.Standings.Keys);
    }

    [Fact]
    public void GroupsAreMatchedExactly_BecauseAnIdentifierIsNotText()
    {
        Reputation reputation = new();
        reputation.Adjust(Miners, 10);

        Assert.Equal(0, reputation.With("Miners"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AGroupNothingCanName_IsRefused(string? group)
    {
        Reputation reputation = new();

        Assert.ThrowsAny<ArgumentException>(() => reputation.With(group!));
        Assert.ThrowsAny<ArgumentException>(() => reputation.Adjust(group!, 1));
    }
}
