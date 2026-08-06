namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="LootMagnet"/>: the two numbers that decide how loot reaches the player, and
/// the values that would leave a drop lying in the world forever being refused where they are
/// authored.
/// </summary>
public sealed class LootMagnetTests
{
    [Fact]
    public void AMagnet_HoldsTheNumbersItWasAuthoredWith()
    {
        LootMagnet magnet = new(Reach: 40, DriftSpeed: 120);

        Assert.Equal(40, magnet.Reach);
        Assert.Equal(120, magnet.DriftSpeed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AReachThatCannotDrawLootIn_IsRefusedWhereItIsAuthored(double reach) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LootMagnet(reach, DriftSpeed: 100));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ADriftSpeedThatNeverArrives_IsRefusedWhereItIsAuthored(double driftSpeed) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LootMagnet(Reach: 40, driftSpeed));
}
