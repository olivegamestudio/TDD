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

    [Fact]
    public void EarningFavour_CannotTurnAGroupHostileThroughOverflow()
    {
        // the sign is the relationship. A standing that wrapped past int.MaxValue would come back
        // maximally hostile, which is a whole game's favour reversed by one award.
        Reputation reputation = new();
        reputation.Adjust(Miners, int.MaxValue);

        Assert.Equal(int.MaxValue, reputation.Adjust(Miners, 1));

        Assert.Equal(int.MaxValue, reputation.With(Miners));
    }

    [Fact]
    public void LosingFavour_CannotTurnAGroupFriendlyThroughUnderflow()
    {
        Reputation reputation = new();
        reputation.Adjust(Miners, int.MinValue);

        Assert.Equal(int.MinValue, reputation.Adjust(Miners, -1));

        Assert.Equal(int.MinValue, reputation.With(Miners));
    }

    [Fact]
    public void OneAdjustmentPastTheEnd_IsHeldAtTheEndRatherThanWrapping()
    {
        // the whole move is clamped, not the last step of it: a single award larger than the range
        // is the same failure as two that add up to more than it
        Reputation reputation = new();
        reputation.Adjust(Miners, 1);

        Assert.Equal(int.MaxValue, reputation.Adjust(Miners, int.MaxValue));
    }

    [Fact]
    public void AStandingHeldAtTheEnd_StillMovesBackTheOtherWay()
    {
        // clamping is a ceiling, not a latch. A group the character has maxed out can still fall
        // out with them.
        Reputation reputation = new();
        reputation.Adjust(Miners, int.MaxValue);
        reputation.Adjust(Miners, 1);

        Assert.Equal(int.MaxValue - 10, reputation.Adjust(Miners, -10));
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
