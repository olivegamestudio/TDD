namespace OliveGameStudio.Tests;

/// <summary>
/// TEMPORARY — deleted in the very next commit on this branch.
///
/// #34 asks for proof that a failing test fails the pull request, and that is the one item on its
/// definition of done that cannot be established by reading the workflow: a YAML file that looks
/// right and a YAML file that actually fails the check are indistinguishable until one of them is
/// made to fail. This test exists to make the check go red once, on the record, so the claim is
/// something anyone can go and look at rather than something the author asserted.
/// </summary>
public class CiGateProofTests
{
    [Fact]
    public void ADeliberatelyFailingTest_TurnsTheCheckRed()
    {
        Assert.Equal(1, 2);
    }
}
