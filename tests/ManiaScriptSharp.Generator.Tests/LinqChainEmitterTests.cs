using System.Linq;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests for LINQ chain → ManiaScript <c>foreach</c> loop desugaring via <c>LinqChainEmitter</c>.
/// All tests use a <c>List&lt;int&gt;</c> (or similar) field so Roslyn resolves the LINQ extension methods.
/// </summary>
public class LinqChainEmitterTests : EmitterTestBase
{
    // Common field declarations used in many tests.
    private const string IntList   = "System.Collections.Generic.List<int> nums = new();";
    private const string IntListMs = "using System.Collections.Generic; using System.Linq; " +
                                     "System.Collections.Generic.List<int> nums = new();";

    private static string StmtWithLinq(string stmt, string extraFields = "")
        => TranslateBodyMs(stmt, $"System.Collections.Generic.List<int> nums = new(); {extraFields}");

    // ── Where → materialize (explicit .ToList()) ────────────────────────────

    [Fact]
    public void Where_Materialize_EmitsForeachWithGuard()
    {
        var output = StmtWithLinq("var filtered = nums.Where(x => x > 0).ToList();");
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("if (X > 0) {", output);
        Assert.Contains("Filtered.add(X);", output);
    }

    [Fact]
    public void Where_Materialize_DeclaresArray()
    {
        var output = StmtWithLinq("var filtered = nums.Where(x => x > 0).ToList();");
        Assert.Contains("declare Integer[] Filtered;", output);
    }

    // ── Select → materialize ────────────────────────────────────────────────

    [Fact]
    public void Select_Materialize_EmitsForeachWithProjection()
    {
        var output = StmtWithLinq("var doubled = nums.Select(x => x * 2).ToList();");
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("Doubled.add(X * 2);", output);
    }

    [Fact]
    public void Select_Materialize_DeclaresArray()
    {
        var output = StmtWithLinq("var doubled = nums.Select(x => x * 2).ToList();");
        Assert.Contains("declare Integer[] Doubled;", output);
    }

    // ── Where + Select ──────────────────────────────────────────────────────

    [Fact]
    public void Where_Select_EmitsFilterThenProject()
    {
        var output = StmtWithLinq("var result = nums.Where(x => x > 0).Select(x => x * 2).ToList();");
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("if (X > 0) {", output);
        Assert.Contains("Result.add(X * 2);", output);
    }

    // ── Distinct ────────────────────────────────────────────────────────────

    [Fact]
    public void Distinct_EmitsExistsCheck()
    {
        var output = StmtWithLinq("var uniq = nums.Distinct().ToList();");
        Assert.Contains("if (!Uniq.exists(Item)) {", output);
        Assert.Contains("Uniq.add(Item);", output);
    }

    // ── Count(pred) ─────────────────────────────────────────────────────────

    [Fact]
    public void Count_WithPredicate_EmitsForeachAccumulator()
    {
        var output = StmtWithLinq("var cnt = nums.Count(x => x > 0);");
        Assert.Contains("declare Integer Cnt = 0;", output);
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("if (X > 0) {", output);
        Assert.Contains("Cnt += 1;", output);
    }

    [Fact]
    public void Where_Count_EmitsForeachAccumulator()
    {
        var output = StmtWithLinq("var cnt = nums.Where(x => x > 5).Count();");
        Assert.Contains("declare Integer Cnt = 0;", output);
        Assert.Contains("if (X > 5) {", output);
        Assert.Contains("Cnt += 1;", output);
    }

    // ── Any(pred) ───────────────────────────────────────────────────────────

    [Fact]
    public void Any_WithPredicate_EmitsBreakOnMatch()
    {
        var output = StmtWithLinq("var any = nums.Any(x => x > 0);");
        Assert.Contains("declare Boolean Any = False;", output);
        Assert.Contains("Any = True;", output);
        Assert.Contains("break;", output);
    }

    // ── All(pred) ───────────────────────────────────────────────────────────

    [Fact]
    public void All_WithPredicate_EmitsBreakOnMismatch()
    {
        var output = StmtWithLinq("var all = nums.All(x => x > 0);");
        Assert.Contains("declare Boolean All = True;", output);
        Assert.Contains("All = False;", output);
        Assert.Contains("break;", output);
    }

    // ── Sum(selector) ───────────────────────────────────────────────────────

    [Fact]
    public void Sum_WithSelector_EmitsAccumulator()
    {
        var output = StmtWithLinq("var total = nums.Sum(x => x * 2);");
        Assert.Contains("declare Integer Total = 0;", output);
        Assert.Contains("Total += X * 2;", output);
    }

    [Fact]
    public void Where_Sum_EmitsGuardedAccumulator()
    {
        var output = StmtWithLinq("var total = nums.Where(x => x > 0).Sum(x => x);");
        Assert.Contains("if (X > 0) {", output);
        Assert.Contains("Total += X;", output);
    }

    // ── First / FirstOrDefault ──────────────────────────────────────────────

    [Fact]
    public void First_WithPredicate_EmitsBreakOnMatch()
    {
        var output = StmtWithLinq("var first = nums.First(x => x > 5);");
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("if (X > 5) {", output);
        Assert.Contains("First = X;", output);
        Assert.Contains("break;", output);
    }

    [Fact]
    public void FirstOrDefault_WithPredicate_EmitsBreakOnMatch()
    {
        var output = StmtWithLinq("var first = nums.FirstOrDefault(x => x > 5);");
        Assert.Contains("First = X;", output);
        Assert.Contains("break;", output);
    }

    // ── Last / LastOrDefault ────────────────────────────────────────────────

    [Fact]
    public void Last_WithPredicate_KeepsOverwritingNoBreak()
    {
        var output = StmtWithLinq("var last = nums.Last(x => x > 0);");
        Assert.Contains("Last = X;", output);
        // No break — iterates all to find the last match.
        Assert.DoesNotContain("break;", output);
    }

    // ── OrderBy ─────────────────────────────────────────────────────────────

    [Fact]
    public void Where_OrderBy_EmitsSortAfterLoop()
    {
        var output = StmtWithLinq("var sorted = nums.Where(x => x > 0).OrderBy(x => x).ToList();");
        Assert.Contains("Sorted.add(X);", output);
        Assert.Contains("Sorted = Sorted.sort();", output);
    }

    [Fact]
    public void Where_OrderByDescending_EmitsSortReverseAfterLoop()
    {
        var output = StmtWithLinq("var sorted = nums.Where(x => x > 0).OrderByDescending(x => x).ToList();");
        Assert.Contains("Sorted = Sorted.sortreverse();", output);
    }

    // ── Select().Where() ────────────────────────────────────────────────────

    [Fact]
    public void Select_Where_EmitsDeclareProjectedThenGuard()
    {
        var output = StmtWithLinq("var result = nums.Select(x => x * 2).Where(y => y > 5).ToList();");
        // projected var declared inside loop
        Assert.Contains("declare Y = X * 2;", output);
        // post-select guard on projected var
        Assert.Contains("if (Y > 5) {", output);
        // add the projected var
        Assert.Contains("Result.add(Y);", output);
    }

    [Fact]
    public void Select_Where_DeclaresCorrectArrayType()
    {
        var output = StmtWithLinq("var result = nums.Select(x => x * 2).Where(y => y > 5).ToList();");
        Assert.Contains("declare Integer[] Result;", output);
    }

    [Fact]
    public void Where_Select_Where_EmitsPreGuardThenProjectThenPostGuard()
    {
        var output = StmtWithLinq("var result = nums.Where(x => x > 0).Select(x => x * 2).Where(y => y < 100).ToList();");
        // pre-select guard uses loop var X
        Assert.Contains("if (X > 0) {", output);
        // projection inside pre-guard
        Assert.Contains("declare Y = X * 2;", output);
        // post-select guard uses projected var Y
        Assert.Contains("if (Y < 100) {", output);
        Assert.Contains("Result.add(Y);", output);
    }

    [Fact]
    public void Select_Where_SameParamName_UsesLoopVarSuffix()
    {
        // Both lambdas use 'x'; projVar should be 'XSel' to avoid collision with loop var 'X'.
        var output = StmtWithLinq("var result = nums.Select(x => x * 2).Where(x => x > 5).ToList();");
        Assert.Contains("declare XSel = X * 2;", output);
        Assert.Contains("if (XSel > 5) {", output);
        Assert.Contains("Result.add(XSel);", output);
    }

    [Fact]
    public void Select_Where_Count_EmitsProjectedVarAndCountsMatches()
    {
        var output = StmtWithLinq("var cnt = nums.Select(x => x * 2).Where(y => y > 5).Count();");
        Assert.Contains("declare Y = X * 2;", output);
        Assert.Contains("if (Y > 5) {", output);
        Assert.Contains("Cnt += 1;", output);
    }

    // ── Deferred/pending chains ─────────────────────────────────────────────
    // A chain without a terminal is registered; code is emitted when a later statement
    // uses that variable with a materialising terminal.

    [Fact]
    public void PendingChain_MaterializedLater_EmitsForeachAtTerminalSite()
    {
        // "evens" is registered as pending (no code), "result" triggers the foreach.
        var output = TranslateBodyMs(
            "var evens = nums.Where(x => x % 2 == 0); var result = evens.ToList();",
            "System.Collections.Generic.List<int> nums = new();");
        // No code for "evens" itself.
        Assert.DoesNotContain("Evens", output);
        // The foreach is emitted for "result".
        Assert.Contains("declare Integer[] Result;", output);
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("if (X % 2 == 0) {", output);
        Assert.Contains("Result.add(X);", output);
    }

    [Fact]
    public void PendingChain_ComposedWithAdditionalStage()
    {
        // Pending Where, then materialise with an extra OrderBy.
        var output = TranslateBodyMs(
            "var evens = nums.Where(x => x % 2 == 0); var sorted = evens.OrderBy(x => x).ToList();",
            "System.Collections.Generic.List<int> nums = new();");
        Assert.DoesNotContain("Evens", output);
        Assert.Contains("declare Integer[] Sorted;", output);
        Assert.Contains("if (X % 2 == 0) {", output);
        Assert.Contains("Sorted = Sorted.sort();", output);
    }

    [Fact]
    public void PendingChain_UsedWithAggregate_EmitsAggregateLoop()
    {
        var output = TranslateBodyMs(
            "var evens = nums.Where(x => x % 2 == 0); var cnt = evens.Count();",
            "System.Collections.Generic.List<int> nums = new();");
        Assert.DoesNotContain("Evens", output);
        Assert.Contains("declare Integer Cnt = 0;", output);
        Assert.Contains("if (X % 2 == 0) {", output);
        Assert.Contains("Cnt += 1;", output);
    }

    [Fact]
    public void PendingChain_ChainedPending_InlinesBoth()
    {
        // q1 → q2 (pending chain of pending chain) → ToList materialises both.
        var output = TranslateBodyMs(
            "var q1 = nums.Where(x => x > 0); var q2 = q1.Where(x => x < 10); var result = q2.ToList();",
            "System.Collections.Generic.List<int> nums = new();");
        Assert.DoesNotContain("Q1", output);
        Assert.DoesNotContain("Q2", output);
        Assert.Contains("declare Integer[] Result;", output);
        // Both Where predicates are combined into a single guard.
        Assert.Contains("if (X > 0 && X < 10) {", output);
    }

    // ── Single / SingleOrDefault ─────────────────────────────────────────────

    [Fact]
    public void Single_WithPredicate_AssertsNoDuplicate_AndRequiresMatch()
    {
        var output = StmtWithLinq("var item = nums.Single(x => x > 5);");
        Assert.Contains("declare Boolean ItemFound = False;", output);
        Assert.Contains("assert(!ItemFound);", output);
        Assert.Contains("Item = X;", output);
        Assert.Contains("ItemFound = True;", output);
        Assert.Contains("assert(ItemFound);", output);
    }

    [Fact]
    public void SingleOrDefault_WithPredicate_AssertsNoDuplicateNoRequireMatch()
    {
        var output = StmtWithLinq("var item = nums.SingleOrDefault(x => x > 5);");
        Assert.Contains("assert(!ItemFound);", output);
        Assert.DoesNotContain("assert(ItemFound);", output);
    }

    // ── ToDictionary ─────────────────────────────────────────────────────────

    [Fact]
    public void ToDictionary_TwoArg_EmitsForeachWithAssertAndAssignment()
    {
        var output = TranslateBodyMs(
            "var map = items.ToDictionary(x => x.Key, x => x.Value);",
            "System.Collections.Generic.List<(int Key, int Value)> items = new();");
        Assert.Contains("foreach (X in Items) {", output);
        Assert.Contains("assert(!Map.existskey(", output);
        Assert.Contains("Map[", output);
    }

    [Fact]
    public void ToDictionary_OneArg_ElementBecomesValue()
    {
        var output = StmtWithLinq("var map = nums.ToDictionary(x => x * 2);");
        Assert.Contains("assert(!Map.existskey(X * 2));", output);
        Assert.Contains("Map[X * 2] = X;", output);
    }

    // ── Contains ─────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_Direct_MapsToExists()
    {
        // Direct List<T>.Contains resolves to the List<T> instance method, not LINQ —
        // ExpressionEmitter maps it to .exists(), which is the correct ManiaScript form.
        var output = StmtWithLinq("var has = nums.Contains(42);");
        Assert.Contains("Nums.exists(42)", output);
    }

    [Fact]
    public void Where_Contains_EmitsGuardedEqualityLoop()
    {
        var output = TranslateBodyMs(
            "var q = nums.Where(x => x > 0); var has = q.Contains(42);",
            "System.Collections.Generic.List<int> nums = new();");
        Assert.Contains("if (X > 0) {", output);
        Assert.Contains("if (X == 42) {", output);
        Assert.Contains("Has = True;", output);
    }

    // ── Aggregate ────────────────────────────────────────────────────────────

    [Fact]
    public void Aggregate_Seeded_EmitsForeachAccumulator()
    {
        var output = StmtWithLinq("var product = nums.Aggregate(1, (acc, x) => acc * x);");
        Assert.Contains("declare Integer Product = 1;", output);
        Assert.Contains("foreach (X in Nums) {", output);
        Assert.Contains("Product = Product * X;", output);
    }

    [Fact]
    public void Aggregate_NoSeed_EmitsForLoopWithAssert()
    {
        var output = StmtWithLinq("var product = nums.Aggregate((acc, x) => acc + x);");
        Assert.Contains("assert(Nums.count > 0);", output);
        Assert.Contains("declare Integer Product = Nums[0];", output);
        Assert.Contains("for (AggI, 1, Nums.count - 1) {", output);
        Assert.Contains("Product = Product + Nums[AggI];", output);
    }

    // ── Min / Max ─────────────────────────────────────────────────────────────

    [Fact]
    public void Min_NoSelector_EmitsForLoopWithAssert()
    {
        var output = StmtWithLinq("var min = nums.Min();");
        Assert.Contains("assert(Nums.count > 0);", output);
        Assert.Contains("declare Integer Min = Nums[0];", output);
        Assert.Contains("for (MinMaxI, 1, Nums.count - 1) {", output);
        Assert.Contains("if (Nums[MinMaxI] < Min) Min = Nums[MinMaxI];", output);
    }

    [Fact]
    public void Max_WithSelector_EmitsForLoopWithAssert()
    {
        var output = StmtWithLinq("var max = nums.Max(x => x * 2);");
        Assert.Contains("assert(Nums.count > 0);", output);
        Assert.Contains("for (MinMaxI, 1, Nums.count - 1) {", output);
        Assert.Contains("if (Val > Max) Max = Val;", output);
    }

    // ── Skip / Take ───────────────────────────────────────────────────────────

    [Fact]
    public void Skip_Take_EmitsForeachWithCounters()
    {
        var output = StmtWithLinq("var page = nums.Skip(10).Take(5).ToList();");
        Assert.Contains("declare Integer[] Page;", output);
        Assert.Contains("if (PageSkipI < 10)", output);
        Assert.Contains("if (Page.count >= 5) break;", output);
    }

    // ── SelectMany ───────────────────────────────────────────────────────────

    [Fact]
    public void SelectMany_CollectionSelectorOnly_EmitsNestedForeach()
    {
        var output = TranslateBodyMs(
            "var flat = items.SelectMany(x => x);",
            "System.Collections.Generic.List<System.Collections.Generic.List<int>> items = new();");
        Assert.Contains("foreach (X in Items) {", output);
        Assert.Contains("foreach (", output);
        Assert.Contains(".add(", output);
    }

    // ── Zip ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Zip_WithResultSelector_EmitsForLoopClamped()
    {
        var output = TranslateBodyMs(
            "var pairs = nums.Zip(strs, (n, s) => n + \": \" + s).ToList();",
            "System.Collections.Generic.List<int> nums = new(); System.Collections.Generic.List<string> strs = new();");
        Assert.Contains("declare ZipCount = Nums.count;", output);
        Assert.Contains("if (Strs.count < ZipCount) ZipCount = Strs.count;", output);
        Assert.Contains("for (ZipI, 0, ZipCount - 1) {", output);
        Assert.Contains("Pairs.add(Nums[ZipI]", output);
        Assert.Contains("Strs[ZipI]", output);
    }
}
