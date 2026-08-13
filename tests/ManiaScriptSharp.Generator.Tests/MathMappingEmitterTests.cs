using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests that System.Math / System.MathF static calls are translated to the
/// correct MathLib:: expressions by <see cref="ManiaScriptSharp.Generator.Emission.ExpressionEmitter"/>.
/// </summary>
public class MathMappingEmitterTests : EmitterTestBase
{
    // ────────── Math direct mappings ──────────

    [Theory]
    [InlineData("Math.Abs(-3)",       "MathLib::Abs(-3)")]
    [InlineData("Math.Abs(-1.5f)",    "MathLib::Abs(-1.5)")]
    [InlineData("Math.Sin(1f)",       "MathLib::Sin(1.)")]
    [InlineData("Math.Cos(1f)",       "MathLib::Cos(1.)")]
    [InlineData("Math.Tan(1f)",       "MathLib::Tan(1.)")]
    [InlineData("Math.Asin(0.5f)",    "MathLib::Asin(0.5)")]
    [InlineData("Math.Acos(0.5f)",    "MathLib::Acos(0.5)")]
    [InlineData("Math.Atan2(1f, 2f)", "MathLib::Atan2(1., 2.)")]
    [InlineData("Math.Exp(2f)",       "MathLib::Exp(2.)")]
    [InlineData("Math.Log(2f)",       "MathLib::Ln(2.)")]
    [InlineData("Math.Sqrt(4f)",      "MathLib::Sqrt(4.)")]
    [InlineData("Math.Pow(2f, 3f)",   "MathLib::Pow(2., 3.)")]
    [InlineData("Math.Max(1, 2)",     "MathLib::Max(1, 2)")]
    [InlineData("Math.Min(1, 2)",     "MathLib::Min(1, 2)")]
    [InlineData("Math.Max(1f, 2f)",   "MathLib::Max(1., 2.)")]
    [InlineData("Math.Min(1f, 2f)",   "MathLib::Min(1., 2.)")]
    [InlineData("Math.Clamp(5, 0, 10)",       "MathLib::Clamp(5, 0, 10)")]
    [InlineData("Math.Clamp(5f, 0f, 10f)",    "MathLib::Clamp(5., 0., 10.)")]
    public void Translate_MathDirectMapping(string input, string expected)
    {
        Assert.Equal(expected, TranslateExpr(input));
    }

    // ────────── MathF mirrors produce identical output ──────────

    [Theory]
    [InlineData("MathF.Abs(-1.5f)",    "MathLib::Abs(-1.5)")]
    [InlineData("MathF.Sin(1f)",       "MathLib::Sin(1.)")]
    [InlineData("MathF.Cos(1f)",       "MathLib::Cos(1.)")]
    [InlineData("MathF.Tan(1f)",       "MathLib::Tan(1.)")]
    [InlineData("MathF.Asin(0.5f)",    "MathLib::Asin(0.5)")]
    [InlineData("MathF.Acos(0.5f)",    "MathLib::Acos(0.5)")]
    [InlineData("MathF.Atan2(1f, 2f)", "MathLib::Atan2(1., 2.)")]
    [InlineData("MathF.Exp(2f)",       "MathLib::Exp(2.)")]
    [InlineData("MathF.Log(2f)",       "MathLib::Ln(2.)")]
    [InlineData("MathF.Sqrt(4f)",      "MathLib::Sqrt(4.)")]
    [InlineData("MathF.Pow(2f, 3f)",   "MathLib::Pow(2., 3.)")]
    [InlineData("MathF.Max(1f, 2f)",   "MathLib::Max(1., 2.)")]
    [InlineData("MathF.Min(1f, 2f)",   "MathLib::Min(1., 2.)")]
    public void Translate_MathFMirror(string input, string expected)
    {
        Assert.Equal(expected, TranslateExpr(input));
    }

    // ────────── Rounding family ──────────

    [Fact]
    public void Translate_MathFloor_MapsToFloorInteger()
    {
        Assert.Equal("MathLib::FloorInteger(2.7)", TranslateExpr("Math.Floor(2.7)"));
    }

    [Fact]
    public void Translate_MathCeiling_MapsToCeilingInteger()
    {
        Assert.Equal("MathLib::CeilingInteger(2.3)", TranslateExpr("Math.Ceiling(2.3)"));
    }

    [Fact]
    public void Translate_MathRound_MapsToNearestInteger()
    {
        // 2.5f → f suffix stripped → 2.5
        Assert.Equal("MathLib::NearestInteger(2.5)", TranslateExpr("Math.Round(2.5f)"));
    }

    [Fact]
    public void Translate_MathTruncate_MapsToTruncInteger()
    {
        Assert.Equal("MathLib::TruncInteger(2.9)", TranslateExpr("Math.Truncate(2.9)"));
    }

    // ────────── Single-arg Atan → Atan2(x, 1.) ──────────

    [Fact]
    public void Translate_MathAtan_BecomesAtan2WithOne()
    {
        Assert.Equal("MathLib::Atan2(1., 1.)", TranslateExpr("Math.Atan(1f)"));
    }

    [Fact]
    public void Translate_MathFAtan_BecomesAtan2WithOne()
    {
        Assert.Equal("MathLib::Atan2(1., 1.)", TranslateExpr("MathF.Atan(1f)"));
    }

    // ────────── Math.Log with base ──────────

    [Fact]
    public void Translate_MathLogWithBase_UsesChangeOfBase()
    {
        // double literals 8.0 / 2.0 already have a decimal point — emitter preserves them as-is
        Assert.Equal("(MathLib::Ln(8.0) / MathLib::Ln(2.0))", TranslateExpr("Math.Log(8.0, 2.0)"));
    }

    [Fact]
    public void Translate_MathLog2_UsesChangeOfBase()
    {
        // user argument 8.0 keeps its form; the hardcoded base "2." is emitted as-is
        Assert.Equal("(MathLib::Ln(8.0) / MathLib::Ln(2.))", TranslateExpr("Math.Log2(8.0)"));
    }

    [Fact]
    public void Translate_MathLog10_UsesChangeOfBase()
    {
        Assert.Equal("(MathLib::Ln(100.0) / MathLib::Ln(10.))", TranslateExpr("Math.Log10(100.0)"));
    }

    // ────────── Derived functions ──────────

    [Fact]
    public void Translate_MathSinh_ExpandedToExpFormula()
    {
        var result = TranslateExpr("Math.Sinh(1f)");
        Assert.Equal("((MathLib::Exp(1.) - MathLib::Exp(-(1.))) / 2.)", result);
    }

    [Fact]
    public void Translate_MathCosh_ExpandedToExpFormula()
    {
        var result = TranslateExpr("Math.Cosh(1f)");
        Assert.Equal("((MathLib::Exp(1.) + MathLib::Exp(-(1.))) / 2.)", result);
    }

    [Fact]
    public void Translate_MathTanh_ExpandedToExpFormula()
    {
        var result = TranslateExpr("Math.Tanh(1f)");
        // tanh(x) = (e^x - e^-x) / (e^x + e^-x)
        Assert.Equal("((MathLib::Exp(1.) - MathLib::Exp(-(1.))) / (MathLib::Exp(1.) + MathLib::Exp(-(1.))))", result);
    }

    [Fact]
    public void Translate_MathCbrt_Positive_UsesPow()
    {
        var result = TranslateExpr("Math.Cbrt(8.0)");
        Assert.Equal("(8.0 >= 0. ? MathLib::Pow(8.0, 0.3333333333333333) : -(MathLib::Pow(-(8.0), 0.3333333333333333)))", result);
    }

    [Fact]
    public void Translate_MathSign_InlineTernary()
    {
        var result = TranslateExpr("Math.Sign(5)");
        Assert.Equal("(5 > 0 ? 1 : (5 < 0 ? -1 : 0))", result);
    }

    // ────────── Static constant properties ──────────

    [Fact]
    public void Translate_MathPI_MapsToMathLibPI()
    {
        Assert.Equal("MathLib::PI()", TranslateExpr("Math.PI"));
    }

    [Fact]
    public void Translate_MathFPI_MapsToMathLibPI()
    {
        Assert.Equal("MathLib::PI()", TranslateExpr("MathF.PI"));
    }

    [Fact]
    public void Translate_MathE_MapsToExp1()
    {
        Assert.Equal("MathLib::Exp(1.)", TranslateExpr("Math.E"));
    }

    [Fact]
    public void Translate_MathFE_MapsToExp1()
    {
        Assert.Equal("MathLib::Exp(1.)", TranslateExpr("MathF.E"));
    }

    [Fact]
    public void Translate_MathTau_MapsToPITimes2()
    {
        Assert.Equal("(MathLib::PI() * 2.)", TranslateExpr("Math.Tau"));
    }
}
