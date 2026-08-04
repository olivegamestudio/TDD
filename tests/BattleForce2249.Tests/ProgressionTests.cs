namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="Progression"/>: what a character has earned, and the states it refuses to hold.
/// </summary>
public sealed class ProgressionTests
{
    [Fact]
    public void ANewCharacter_StartsAtLevelOneWithNothingEarned()
    {
        Progression progression = new();

        // level one rather than zero: the first level is something they are, not something they
        // have yet to earn
        Assert.Equal(1, progression.Level);
        Assert.Equal(0, progression.Experience);
        Assert.Equal(0, progression.SpendPoints);
        Assert.Empty(progression.Gifts);
    }

    [Fact]
    public void Gain_AddsToWhatHasBeenEarned()
    {
        Progression progression = new();

        progression.Gain(120);
        progression.Gain(30);

        Assert.Equal(150, progression.Experience);
    }

    [Fact]
    public void EarningExperience_DoesNotOnItsOwnRaiseTheLevel()
    {
        // no curve is stated anywhere yet, and a curve invented here would be a number the design
        // then has to argue with. Levelling is asked for, not inferred.
        Progression progression = new();

        progression.Gain(1_000_000);

        Assert.Equal(1, progression.Level);
        Assert.Equal(0, progression.SpendPoints);
    }

    [Fact]
    public void Advance_RaisesTheLevelAndHandsOverThePointsItIsWorth()
    {
        Progression progression = new();

        progression.Advance(spendPoints: 3);

        Assert.Equal(2, progression.Level);
        Assert.Equal(3, progression.SpendPoints);
    }

    [Fact]
    public void Spend_TakesFromThePointsHeld()
    {
        Progression progression = new();
        progression.Advance(spendPoints: 3);

        progression.Spend(2);

        Assert.Equal(1, progression.SpendPoints);
    }

    [Fact]
    public void SpendingMorePointsThanAreHeld_IsRefused()
    {
        // a character owing points is not a state anything else knows how to read
        Progression progression = new();
        progression.Advance(spendPoints: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => progression.Spend(2));
        Assert.Equal(1, progression.SpendPoints);
    }

    [Fact]
    public void EarningANegativeAmount_IsRefused()
    {
        // experience is not taken back through the door it was earned through; a penalty needs
        // saying out loud, because the moment anything reports what was earned the two differ
        Progression progression = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => progression.Gain(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => progression.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => progression.Spend(-1));
        Assert.Equal(0, progression.Experience);
        Assert.Equal(1, progression.Level);
    }

    [Fact]
    public void EarningPastTheCeiling_HoldsAtTheCeilingRatherThanWrappingNegative()
    {
        // a character who has earned everything has not earned less than nothing: wrapping would
        // hand the record back a number no reporting code can read
        Progression progression = new();
        progression.Gain(int.MaxValue);

        progression.Gain(1);

        Assert.Equal(int.MaxValue, progression.Experience);
    }

    [Fact]
    public void AdvancingPastThePointCeiling_HoldsAtTheCeilingRatherThanWrappingNegative()
    {
        // wrapping would leave the character owing points — the state Spend exists to refuse
        Progression progression = new();
        progression.Advance(int.MaxValue);

        progression.Advance(1);

        Assert.Equal(int.MaxValue, progression.SpendPoints);
    }

    [Fact]
    public void PointsHeldAtTheCeiling_CanStillBeSpent()
    {
        // the ceiling is a ceiling, not a lock: a wrapped negative total would have made every
        // future Spend throw, because the negative reads as the amount held
        Progression progression = new();
        progression.Advance(int.MaxValue);
        progression.Advance(1);

        progression.Spend(2);

        Assert.Equal(int.MaxValue - 2, progression.SpendPoints);
    }

    // Level's own ceiling is guarded by the same rule, but has no test here: the only way to raise
    // a level is to ask for one, so reaching int.MaxValue means int.MaxValue calls to Advance. A
    // test that takes minutes to prove a line of arithmetic is not worth the suite's time.

    [Fact]
    public void Grant_GivesAGiftTheCharacterDidNotHave()
    {
        Progression progression = new();

        Assert.True(progression.Grant("steady-hands"));

        Assert.True(progression.Has("steady-hands"));
        Assert.Equal(["steady-hands"], progression.Gifts);
    }

    [Fact]
    public void GrantingAGiftTwice_IsNotTwiceTheGift()
    {
        Progression progression = new();
        progression.Grant("steady-hands");

        Assert.False(progression.Grant("steady-hands"));

        Assert.Single(progression.Gifts);
    }

    [Fact]
    public void AGiftIsMatchedExactly_BecauseAnIdentifierIsNotText()
    {
        // identifiers are never translated and never case folded: a gift written to a save on one
        // machine has to be the same gift on another
        Progression progression = new();
        progression.Grant("steady-hands");

        Assert.False(progression.Has("Steady-Hands"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AGiftNothingCanName_IsRefused(string? giftId)
    {
        Progression progression = new();

        Assert.ThrowsAny<ArgumentException>(() => progression.Grant(giftId!));
        Assert.ThrowsAny<ArgumentException>(() => progression.Has(giftId!));
    }
}
