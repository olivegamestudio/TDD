namespace OliveGameStudio.Tests;

/// <summary>
/// Adversarial coverage added by QA while verifying #13.
/// </summary>
/// <remarks>
/// <para>
/// #13 made elements identities rather than values, and made <see cref="UIController.Add"/> refuse
/// a button it already holds. These tests attack both from the outside: the entry points the fix
/// did <i>not</i> name, the sibling ordering that would let a name match give the right answer by
/// luck, and the type-level APIs the change claims are now safe.
/// </para>
/// <para>
/// Where a same-named sibling is involved, the sibling is deliberately added <b>first</b>. Every
/// lookup returns the first matching node, so a controller that still compared names would resolve
/// to the sibling — which means these tests fail if the identity rule is ever weakened, rather than
/// passing by accident of registration order.
/// </para>
/// </remarks>
public sealed class UIControllerIdentityAdversarialTests
{
    // ---- the double-add guard ----------------------------------------------

    [Fact]
    public void ARefusedDoubleAdd_LeavesTheButtonWorking()
    {
        // the guard runs before anything is recorded, so the refusal must not leave the
        // controller half-updated: the button that was already there still presses, once.
        UIController controller = new();
        Button start = new("START");
        controller.Add(start);

        List<string> committed = [];
        controller.OnReleased(start, () => committed.Add("start"));

        Assert.Throws<InvalidOperationException>(() => controller.Add(start));

        controller.Press(start);
        controller.Release();

        Assert.Equal(["start"], committed);     // exactly once — no second node was left behind
        Assert.True(controller.IsEnabled(start));
        Assert.Same(start, controller.Focused);
    }

    [Fact]
    public void TheDoubleAddGuard_DoesNotMistakeASameNamedButtonForTheSameButton()
    {
        // the guard has to match the button, not its name. A guard written with name
        // equality would turn #13's whole scenario — two screens each labelling a button
        // BACK — into an exception at startup.
        UIController controller = new();

        controller.Add(new Button("BACK"));

        Exception? thrown = Record.Exception(() => controller.Add(new Button("BACK")));

        Assert.Null(thrown);
    }

    [Fact]
    public void TheDoubleAddGuard_DoesNotReachElementsThatAreNotButtons()
    {
        // only buttons become nodes, so only buttons can collide. Nothing about an image or
        // a label should be refused on account of the guard.
        UIController controller = new();

        Exception? thrown = Record.Exception(() =>
        {
            controller.Add(new Image("BACKGROUND"));
            controller.Add(new Image("BACKGROUND"));
            controller.Add(new Text("Continue"));
            controller.Add(new Text("Continue"));
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void ADoubleAdd_IsRefusedEvenWhenTheButtonHasBeenDisabled()
    {
        // the node is still there, still unreachable if a second were added beside it
        UIController controller = new();
        Button start = new("START");
        controller.Add(start);
        controller.Disable(start);

        Assert.Throws<InvalidOperationException>(() => controller.Add(start));
    }

    // ---- every entry point, not just the three the issue named --------------

    [Fact]
    public void AStrangerSharingAName_IsRefusedByEveryEntryPoint()
    {
        // #13's reproduction covered IsEnabled, Disable and Link. These are the rest of the
        // public surface that resolves a button, and they have to agree.
        UIController controller = new();
        Button managed = new("BACK");
        controller.Add(managed);

        Button stranger = new("BACK");

        Assert.Throws<InvalidOperationException>(() => controller.OnPressed(stranger, () => { }));
        Assert.Throws<InvalidOperationException>(() => controller.OnReleased(stranger, () => { }));
        Assert.Throws<InvalidOperationException>(() => controller.Press(stranger));
        Assert.Throws<InvalidOperationException>(() => controller.Enable(stranger));
        Assert.Throws<InvalidOperationException>(() => controller.IsEnabled(stranger));
    }

    [Fact]
    public void FocusingAStrangerThatSharesAName_DoesNotPressTheManagedButton()
    {
        // FocusOn does not validate, so focus can be aimed at a button the controller does
        // not hold. Pressing it must not quietly find the managed button of the same name
        // and fire that instead — which is exactly what a name match did.
        UIController controller = new();
        Button managed = new("BACK");
        controller.Add(managed);

        bool fired = false;
        controller.OnPressed(managed, () => fired = true);

        controller.FocusOn(new Button("BACK"));

        Assert.Throws<InvalidOperationException>(() => controller.Press());
        Assert.False(fired);
    }

    // ---- siblings, with the sibling registered first ------------------------

    [Fact]
    public void ReleaseCommitsThePressedButton_WhenItsSameNamedSiblingWasAddedFirst()
    {
        // registration order is the whole point: a name match resolves to the first node, so
        // the sibling is added first to make sure this passes for the right reason.
        UIController controller = new();
        Button optionsBack = new("BACK");
        Button menuBack = new("BACK");
        controller.Add(optionsBack);
        controller.Add(menuBack);

        List<string> committed = [];
        controller.OnReleased(optionsBack, () => committed.Add("options"));
        controller.OnReleased(menuBack, () => committed.Add("menu"));

        controller.Press(menuBack);
        controller.Release();

        Assert.Equal(["menu"], committed);
    }

    [Fact]
    public void APressActionThatMovesFocusToASameNamedSibling_StillCommitsThePressedButton()
    {
        // #18 settled that the press belongs to the button that was pressed, not to whatever
        // is focused when the pressed action returns. #13 is what makes "which button" a
        // question with an answer when both are called BACK.
        UIController controller = new();
        Button optionsBack = new("BACK");
        Button menuBack = new("BACK");
        controller.Add(optionsBack);
        controller.Add(menuBack);

        List<string> committed = [];
        controller.OnPressed(menuBack, () => controller.FocusOn(optionsBack));
        controller.OnReleased(optionsBack, () => committed.Add("options"));
        controller.OnReleased(menuBack, () => committed.Add("menu"));

        controller.Press(menuBack);

        Assert.Same(optionsBack, controller.Focused);   // the action moved focus...
        Assert.Same(menuBack, controller.Held);         // ...but the hold stayed put

        controller.Release();

        Assert.Equal(["menu"], committed);
    }

    [Fact]
    public void APressActionThatDisablesItsOwnButton_DoesNotCommitItsSameNamedSibling()
    {
        // disabling the held button re-homes focus onto the sibling. The release reads the
        // held button's enablement, so nothing commits — and in particular the sibling,
        // which is now focused and shares the name, must not commit in its place.
        UIController controller = new();
        Button optionsBack = new("BACK");
        Button menuBack = new("BACK");
        controller.Add(optionsBack);
        controller.Add(menuBack);

        List<string> committed = [];
        controller.OnPressed(menuBack, () => controller.Disable(menuBack));
        controller.OnReleased(optionsBack, () => committed.Add("options"));
        controller.OnReleased(menuBack, () => committed.Add("menu"));

        controller.Press(menuBack);
        controller.Release();

        Assert.Empty(committed);
        Assert.Same(optionsBack, controller.Focused);
    }

    [Fact]
    public void CancellingAPress_DoesNotLeaveTheSameNamedSiblingHeld()
    {
        UIController controller = new();
        Button optionsBack = new("BACK");
        Button menuBack = new("BACK");
        controller.Add(optionsBack);
        controller.Add(menuBack);

        List<string> committed = [];
        controller.OnReleased(optionsBack, () => committed.Add("options"));
        controller.OnReleased(menuBack, () => committed.Add("menu"));

        controller.Press(menuBack);
        controller.Cancel();
        controller.Release();

        Assert.Null(controller.Held);
        Assert.Empty(committed);
    }

    // ---- focus re-homing between siblings -----------------------------------

    [Fact]
    public void DisablingTheFocusedButton_ReHomesOntoTheSibling_NotBackOntoItself()
    {
        // SetEnabled re-homes to the first enabled node. With two buttons named BACK the
        // question "is the focused one the one I disabled?" only has an answer under identity.
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        Assert.Same(menuBack, controller.Focused);

        controller.Disable(menuBack);

        Assert.Same(optionsBack, controller.Focused);
        Assert.False(controller.IsEnabled(menuBack));
        Assert.True(controller.IsEnabled(optionsBack));
    }

    [Fact]
    public void EnablingWhileNothingIsFocused_AdoptsTheButtonEnabled_NotItsSibling()
    {
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        controller.Disable(menuBack);
        controller.Disable(optionsBack);

        Assert.Null(controller.Focused);

        controller.Enable(optionsBack);

        Assert.Same(optionsBack, controller.Focused);
    }

    [Fact]
    public void ManySameNamedButtons_AreEachAddressable()
    {
        // two is the reproduction; the rule is not "two". Disabling any one of five leaves
        // the other four alone.
        UIController controller = new();
        List<Button> backs = [.. Enumerable.Range(0, 5).Select(_ => new Button("BACK"))];
        foreach (Button back in backs)
        {
            controller.Add(back);
        }

        controller.Disable(backs[3]);

        Assert.False(controller.IsEnabled(backs[3]));
        Assert.All(backs.Where((_, i) => i != 3), b => Assert.True(controller.IsEnabled(b)));
    }

    [Fact]
    public void OneButtonInTwoControllers_IsTrackedSeparatelyByEach()
    {
        // the singleton is the shipping arrangement, not a constraint of the type. A button
        // held by two controllers has two states, and neither controller speaks for the other.
        Button shared = new("BACK");
        UIController first = new();
        UIController second = new();
        first.Add(shared);
        second.Add(shared);

        first.Disable(shared);

        Assert.False(first.IsEnabled(shared));
        Assert.True(second.IsEnabled(shared));
    }

    // ---- links resolve to the node, not to the name -------------------------

    [Fact]
    public void LinkingToASameNamedSibling_WiresTheIntendedDestination()
    {
        // both ends of Link resolve through the same lookup. Wiring menu → options between
        // two buttons named BACK has to reach the second node, and relinking the same
        // direction must not be refused as a duplicate.
        UIController controller = new();
        Button optionsBack = new("BACK");
        Button menuBack = new("BACK");
        controller.Add(optionsBack);
        controller.Add(menuBack);

        Exception? thrown = Record.Exception(() =>
        {
            controller.Link(menuBack, Direction.Down, optionsBack);
            controller.Link(optionsBack, Direction.Up, menuBack);
            controller.Link(menuBack, Direction.Down, menuBack);   // a button may link to itself
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void LinkingToAStrangerSharingAName_IsRefused()
    {
        UIController controller = new();
        Button managed = new("BACK");
        controller.Add(managed);

        Assert.Throws<InvalidOperationException>(
            () => controller.Link(managed, Direction.Down, new Button("BACK")));
        Assert.Throws<InvalidOperationException>(
            () => controller.Link(new Button("BACK"), Direction.Down, managed));
    }

    // ---- names that are not names -------------------------------------------

    [Fact]
    public void ButtonsWithBlankNames_AreStillTwoButtons()
    {
        // an unlabelled button is the worst case for name matching: every one of them
        // collides with every other.
        UIController controller = new();
        Button first = new("");
        Button second = new("");
        controller.Add(first);
        controller.Add(second);

        controller.Disable(second);

        Assert.True(controller.IsEnabled(first));
        Assert.False(controller.IsEnabled(second));
    }

    [Fact]
    public void AStrangerWithANullName_IsReportedRatherThanCrashing()
    {
        // Require builds its message from button.Name. A null name must produce the
        // InvalidOperationException the caller expects, not a NullReferenceException from
        // the diagnostic that was meant to explain it.
        UIController controller = new();
        controller.Add(new Button("BACK"));

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => controller.IsEnabled(new Button(null!)));

        Assert.NotNull(thrown.Message);
    }

    [Fact]
    public void ADoubleAddOfAButtonWithANullName_IsReportedRatherThanCrashing()
    {
        UIController controller = new();
        Button unnamed = new(null!);
        controller.Add(unnamed);

        Assert.Throws<InvalidOperationException>(() => controller.Add(unnamed));
    }

    // ---- the type contract, across the APIs the fix relies on ----------------

    [Fact]
    public void AButtonEqualsItself_AndNothingElse()
    {
        Button back = new("BACK");

        Assert.True(back.Equals(back));
        Assert.True(back == back);
        Assert.False(back.Equals(new Button("BACK")));
        Assert.False(back.Equals(null));
        Assert.Equal(back.GetHashCode(), back.GetHashCode());   // stable across calls
    }

    [Fact]
    public void SameNamedButtons_AreDistinctToEveryCollectionApiTheControllerMightUse()
    {
        // the argument for changing the type rather than the three lookups was that the next
        // ==, Contains, IndexOf, Distinct or dictionary would otherwise bring the bug back.
        // This is that claim, tested.
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        List<Button> buttons = [menuBack, optionsBack];

        Assert.DoesNotContain(optionsBack, new List<Button> { menuBack });
        Assert.Equal(0, buttons.IndexOf(menuBack));
        Assert.Equal(1, buttons.IndexOf(optionsBack));
        Assert.Equal(2, buttons.Distinct().Count());
        Assert.Equal(2, new HashSet<Button>(buttons).Count);

        buttons.Remove(optionsBack);

        Assert.Same(menuBack, Assert.Single(buttons));
    }

    [Fact]
    public void ElementsOfDifferentKindsSharingAName_AreNotEqual()
    {
        Assert.False(new Button("BACK").Equals(new Image("BACK")));
        Assert.False(new Image("BACK").Equals(new Text("BACK")));
    }
}
