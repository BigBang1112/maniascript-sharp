using System;
using Xunit;

namespace ManiaScriptSharp.ManiaPlanet.Tests;

/// <summary>
/// Unit tests for the <see cref="MathLib"/> C# runtime implementation.
/// Covers every public partial method added in MathLib.cs.
/// </summary>
public class MathLibTests
{
    private static readonly MathLib _lib = new();

    // ────────── Abs ──────────

    [Theory]
    [InlineData(5,   5)]
    [InlineData(-5,  5)]
    [InlineData(0,   0)]
    public void Abs_Int(int input, int expected) =>
        Assert.Equal(expected, _lib.Abs(input));

    [Theory]
    [InlineData( 2.5f,  2.5f)]
    [InlineData(-2.5f,  2.5f)]
    [InlineData( 0f,    0f)]
    public void Abs_Float(float input, float expected) =>
        Assert.Equal(expected, _lib.Abs(input));

    // ────────── ToReal / NearestReal ──────────

    [Theory]
    [InlineData(3,  3f)]
    [InlineData(-1, -1f)]
    [InlineData(0,  0f)]
    public void ToReal_ConvertsIntToFloat(int input, float expected) =>
        Assert.Equal(expected, _lib.ToReal(input));

    [Theory]
    [InlineData(3,  3f)]
    [InlineData(-2, -2f)]
    public void NearestReal_ConvertsIntToFloat(int input, float expected) =>
        Assert.Equal(expected, _lib.NearestReal(input));

    // ────────── DegToRad / RadToDeg ──────────

    [Fact]
    public void DegToRad_180_IsPI()
    {
        Assert.Equal((float)Math.PI, _lib.DegToRad(180f), 5);
    }

    [Fact]
    public void DegToRad_0_Is0()
    {
        Assert.Equal(0f, _lib.DegToRad(0f));
    }

    [Fact]
    public void RadToDeg_PI_Is180()
    {
        Assert.Equal(180f, _lib.RadToDeg((float)Math.PI), 4);
    }

    [Fact]
    public void RadToDeg_DegToRad_RoundTrip()
    {
        Assert.Equal(45f, _lib.RadToDeg(_lib.DegToRad(45f)), 4);
    }

    // ────────── Trig functions ──────────

    [Fact]
    public void Sin_Zero_IsZero() =>
        Assert.Equal(0f, _lib.Sin(0f), 6);

    [Fact]
    public void Sin_HalfPi_IsOne() =>
        Assert.Equal(1f, _lib.Sin((float)(Math.PI / 2)), 5);

    [Fact]
    public void Cos_Zero_IsOne() =>
        Assert.Equal(1f, _lib.Cos(0f), 6);

    [Fact]
    public void Cos_Pi_IsMinusOne() =>
        Assert.Equal(-1f, _lib.Cos((float)Math.PI), 5);

    [Fact]
    public void Tan_Zero_IsZero() =>
        Assert.Equal(0f, _lib.Tan(0f), 6);

    [Fact]
    public void Atan2_OneOne_IsQuarterPI()
    {
        Assert.Equal((float)(Math.PI / 4), _lib.Atan2(1f, 1f), 5);
    }

    [Fact]
    public void Asin_One_IsHalfPI()
    {
        Assert.Equal((float)(Math.PI / 2), _lib.Asin(1f), 5);
    }

    [Fact]
    public void Acos_One_IsZero()
    {
        Assert.Equal(0f, _lib.Acos(1f), 5);
    }

    // ────────── Exp / Ln / Sqrt / Pow ──────────

    [Fact]
    public void Exp_Zero_IsOne() =>
        Assert.Equal(1f, _lib.Exp(0f), 6);

    [Fact]
    public void Exp_One_IsE()
    {
        Assert.Equal((float)Math.E, _lib.Exp(1f), 5);
    }

    [Fact]
    public void Ln_One_IsZero() =>
        Assert.Equal(0f, _lib.Ln(1f), 6);

    [Fact]
    public void Ln_E_IsOne()
    {
        Assert.Equal(1f, _lib.Ln((float)Math.E), 5);
    }

    [Fact]
    public void Sqrt_Four_IsTwo() =>
        Assert.Equal(2f, _lib.Sqrt(4f), 6);

    [Fact]
    public void Pow_TwoThree_IsEight() =>
        Assert.Equal(8f, _lib.Pow(2f, 3f), 5);

    // ────────── PI ──────────

    [Fact]
    public void PI_EqualsSystemMathPI()
    {
        Assert.Equal((float)Math.PI, _lib.PI(), 6);
    }

    // ────────── Rounding ──────────

    [Theory]
    [InlineData(2.9f,  3)]
    [InlineData(2.5f,  3)]   // AwayFromZero
    [InlineData(2.1f,  2)]
    [InlineData(-2.1f, -2)]
    [InlineData(-2.5f, -3)]  // AwayFromZero
    public void NearestInteger_RoundsAwayFromZero(float input, int expected) =>
        Assert.Equal(expected, _lib.NearestInteger(input));

    [Theory]
    [InlineData(2.9f,  2)]
    [InlineData(-2.1f, -3)]
    public void FloorInteger_FloorsTowardNegInf(float input, int expected) =>
        Assert.Equal(expected, _lib.FloorInteger(input));

    [Theory]
    [InlineData(2.1f,  3)]
    [InlineData(-2.9f, -2)]
    public void CeilingInteger_CeilsTowardPosInf(float input, int expected) =>
        Assert.Equal(expected, _lib.CeilingInteger(input));

    [Theory]
    [InlineData(2.9f,   2)]
    [InlineData(-2.9f, -2)]
    public void TruncInteger_TruncatesTowardZero(float input, int expected) =>
        Assert.Equal(expected, _lib.TruncInteger(input));

    // ────────── Rand ──────────

    [Fact]
    public void Rand_IntInclusive_InRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var r = _lib.Rand(1, 6);
            Assert.InRange(r, 1, 6);
        }
    }

    [Fact]
    public void Rand_IntWithSeed_Deterministic()
    {
        var r1 = _lib.Rand(0, 100, 42);
        var r2 = _lib.Rand(0, 100, 42);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Rand_FloatInRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var r = _lib.Rand(0f, 1f);
            Assert.InRange(r, 0f, 1f);
        }
    }

    [Fact]
    public void Rand_FloatWithSeed_Deterministic()
    {
        var r1 = _lib.Rand(0f, 10f, 99);
        var r2 = _lib.Rand(0f, 10f, 99);
        Assert.Equal(r1, r2);
    }

    // ────────── Distance ──────────

    [Fact]
    public void Distance_Float_AbsDiff()
    {
        Assert.Equal(3f, _lib.Distance(1f, 4f), 6);
        Assert.Equal(3f, _lib.Distance(4f, 1f), 6);
    }

    [Fact]
    public void Distance_Vec2_PythagoreanTriple()
    {
        // 3-4-5 right triangle
        Assert.Equal(5f, _lib.Distance(new Vec2(0, 0), new Vec2(3, 4)), 5);
    }

    [Fact]
    public void Distance_Vec3_PythagoreanExample()
    {
        // √(1²+2²+2²) = √9 = 3
        Assert.Equal(3f, _lib.Distance(new Vec3(0, 0, 0), new Vec3(1, 2, 2)), 5);
    }

    // ────────── DotProduct / CrossProduct ──────────

    [Fact]
    public void DotProduct_Perpendicular_IsZero()
    {
        Assert.Equal(0f, _lib.DotProduct(new Vec3(1, 0, 0), new Vec3(0, 1, 0)), 6);
    }

    [Fact]
    public void DotProduct_Parallel_IsMagnitudeProduct()
    {
        Assert.Equal(9f, _lib.DotProduct(new Vec3(3, 0, 0), new Vec3(3, 0, 0)), 6);
    }

    [Fact]
    public void CrossProduct_XY_IsZ()
    {
        var result = _lib.CrossProduct(new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        Assert.Equal(0f, result.X, 6);
        Assert.Equal(0f, result.Y, 6);
        Assert.Equal(1f, result.Z, 6);
    }

    [Fact]
    public void CrossProduct_SameVector_IsZeroVector()
    {
        var v = new Vec3(1, 2, 3);
        var result = _lib.CrossProduct(v, v);
        Assert.Equal(0f, result.X, 5);
        Assert.Equal(0f, result.Y, 5);
        Assert.Equal(0f, result.Z, 5);
    }

    // ────────── Angle ──────────

    [Fact]
    public void Angle_Vec3_SameDirection_IsZero()
    {
        var v = new Vec3(1, 0, 0);
        Assert.Equal(0f, _lib.Angle(v, v), 5);
    }

    [Fact]
    public void Angle_Vec3_Perpendicular_IsHalfPI()
    {
        Assert.Equal((float)(Math.PI / 2), _lib.Angle(new Vec3(1, 0, 0), new Vec3(0, 1, 0)), 5);
    }

    [Fact]
    public void Angle_Vec2_Perpendicular_IsHalfPI()
    {
        Assert.Equal((float)(Math.PI / 2), _lib.Angle(new Vec2(1, 0), new Vec2(0, 1)), 5);
    }

    [Fact]
    public void Angle_Radians_SmallestAngle()
    {
        // Smallest angle from π to 0 is π (half-turn)
        Assert.Equal((float)Math.PI, _lib.Angle((float)Math.PI, 0f), 5);
    }

    [Fact]
    public void OrientedAngle_Vec3_Counterclockwise_Positive()
    {
        // X → Y (counterclockwise in XY plane)
        var angle = _lib.OrientedAngle(new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        Assert.Equal((float)(Math.PI / 2), angle, 5);
    }

    [Fact]
    public void OrientedAngle_Vec2_CounterclockwiseIsPositive()
    {
        var angle = _lib.OrientedAngle(new Vec2(1, 0), new Vec2(0, 1));
        Assert.Equal((float)(Math.PI / 2), angle, 5);
    }

    // ────────── Max / Min / Clamp / Mod ──────────

    [Theory]
    [InlineData(3, 5, 5)]
    [InlineData(5, 3, 5)]
    [InlineData(4, 4, 4)]
    public void Max_Int(int a, int b, int expected) =>
        Assert.Equal(expected, _lib.Max(a, b));

    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(5, 3, 3)]
    [InlineData(4, 4, 4)]
    public void Min_Int(int a, int b, int expected) =>
        Assert.Equal(expected, _lib.Min(a, b));

    [Theory]
    [InlineData(5,  0, 10,  5)]
    [InlineData(-1, 0, 10,  0)]
    [InlineData(15, 0, 10, 10)]
    public void Clamp_Int(int x, int lo, int hi, int expected) =>
        Assert.Equal(expected, _lib.Clamp(x, lo, hi));

    [Theory]
    [InlineData(3f,  5f, 5f)]
    [InlineData(5f,  3f, 5f)]
    public void Max_Float(float a, float b, float expected) =>
        Assert.Equal(expected, _lib.Max(a, b));

    [Theory]
    [InlineData(3f,  5f, 3f)]
    [InlineData(5f,  3f, 3f)]
    public void Min_Float(float a, float b, float expected) =>
        Assert.Equal(expected, _lib.Min(a, b));

    [Theory]
    [InlineData(5f,  0f, 10f,  5f)]
    [InlineData(-1f, 0f, 10f,  0f)]
    [InlineData(15f, 0f, 10f, 10f)]
    public void Clamp_Float(float x, float lo, float hi, float expected) =>
        Assert.Equal(expected, _lib.Clamp(x, lo, hi));

    [Fact]
    public void Mod_WrapsIntoRange()
    {
        // Mod(11, 0, 10) = 1  (range 0–10, span 10)
        Assert.Equal(1f, _lib.Mod(11f, 0f, 10f), 5);
    }

    [Fact]
    public void Mod_ValueAtMin_IsMin()
    {
        Assert.Equal(0f, _lib.Mod(0f, 0f, 10f), 5);
    }

    [Fact]
    public void Mod_NegativeValue_WrapsCorrectly()
    {
        // Mod(-1, 0, 10) = 9
        Assert.Equal(9f, _lib.Mod(-1f, 0f, 10f), 5);
    }
}
