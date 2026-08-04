namespace Pilgrimage.Tests;

public sealed class QuestTriggerTests
{
    [Fact]
    public void DescribesWhatFiresItAndHowClose()
    {
        QuestTrigger trigger = new(QuestTriggerKind.Proximity, 50);

        Assert.Equal(QuestTriggerKind.Proximity, trigger.Kind);
        Assert.Equal(50, trigger.Distance);
    }

    [Fact]
    public void RejectsANegativeDistance()
    {
        // no position could ever satisfy it, so it would silently never fire
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuestTrigger(QuestTriggerKind.Proximity, -1));
    }

    [Fact]
    public void RejectsAnInfiniteDistance()
    {
        // every position satisfies it, so it fires the instant the player exists
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QuestTrigger(QuestTriggerKind.Proximity, double.PositiveInfinity));
    }

    [Fact]
    public void RejectsANegativelyInfiniteDistance()
    {
        // the negative guard already stopped this; it is pinned so the finite rule keeps stopping it
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QuestTrigger(QuestTriggerKind.Proximity, double.NegativeInfinity));
    }

    [Fact]
    public void RejectsADistanceThatIsNotANumber()
    {
        // no comparison against NaN is ever true, so this is the never-fires case wearing a disguise
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QuestTrigger(QuestTriggerKind.Proximity, double.NaN));
    }

    [Fact]
    public void AllowsAVeryLargeFiniteDistance()
    {
        // the world is unbounded, so distance is not what is being refused — only a number that is not one
        QuestTrigger trigger = new(QuestTriggerKind.Proximity, double.MaxValue);

        Assert.Equal(double.MaxValue, trigger.Distance);
    }

    [Fact]
    public void AllowsAZeroDistance()
    {
        QuestTrigger trigger = new(QuestTriggerKind.Proximity, 0);

        Assert.Equal(0, trigger.Distance);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 25));

        Assert.NotEqual(
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));
    }
}
