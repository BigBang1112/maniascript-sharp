using System;

namespace ManiaScriptSharp;

public sealed partial class MathLib
{
    // Thread-local random instance so parallel calls don't share state.
    [ThreadStatic]
    private static Random? _threadRandom;
    private static Random ThreadRandom => _threadRandom ??= new Random();

    public partial int Abs(int Argument1) => Math.Abs(Argument1);
    public partial float Abs(float Argument1) => (float)Math.Abs((double)Argument1);

    public partial float ToReal(int Argument1) => (float)Argument1;

    public partial float DegToRad(float Argument1) => Argument1 * (float)(Math.PI / 180.0);
    public partial float RadToDeg(float Argument1) => Argument1 * (float)(180.0 / Math.PI);

    public partial float Sin(float Argument1) => (float)Math.Sin(Argument1);
    public partial float Cos(float Argument1) => (float)Math.Cos(Argument1);
    public partial float Tan(float Argument1) => (float)Math.Tan(Argument1);
    public partial float Atan2(float Argument1, float Argument2) => (float)Math.Atan2(Argument1, Argument2);
    public partial float Asin(float Argument1) => (float)Math.Asin(Argument1);
    public partial float Acos(float Argument1) => (float)Math.Acos(Argument1);

    public partial float Exp(float Argument1) => (float)Math.Exp(Argument1);
    public partial float Ln(float Argument1) => (float)Math.Log(Argument1);
    public partial float Sqrt(float Argument1) => (float)Math.Sqrt(Argument1);
    public partial float Pow(float Argument1, float Argument2) => (float)Math.Pow(Argument1, Argument2);

    public partial float PI() => (float)Math.PI;

    public partial float NearestReal(int Argument1) => (float)Argument1;
    public partial int NearestInteger(float Argument1) => (int)Math.Round((double)Argument1, MidpointRounding.AwayFromZero);
    public partial int FloorInteger(float Argument1) => (int)Math.Floor((double)Argument1);
    public partial int TruncInteger(float Argument1) => (int)Math.Truncate((double)Argument1);
    public partial int CeilingInteger(float Argument1) => (int)Math.Ceiling((double)Argument1);

    public partial float Rand(float Argument1, float Argument2) =>
        (float)(Argument1 + ThreadRandom.NextDouble() * (Argument2 - Argument1));

    public partial float Rand(float Argument1, float Argument2, int Argument3) =>
        (float)(Argument1 + new Random(Argument3).NextDouble() * (Argument2 - Argument1));

    public partial int Rand(int Argument1, int Argument2) =>
        ThreadRandom.Next(Argument1, Argument2 + 1);

    public partial int Rand(int Argument1, int Argument2, int Argument3) =>
        new Random(Argument3).Next(Argument1, Argument2 + 1);

    public partial float Distance(float Argument1, float Argument2) =>
        (float)Math.Abs((double)(Argument2 - Argument1));

    public partial float Distance(Vec2 Argument1, Vec2 Argument2)
    {
        var dx = Argument2.X - Argument1.X;
        var dy = Argument2.Y - Argument1.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public partial float Distance(Vec3 Argument1, Vec3 Argument2)
    {
        var dx = Argument2.X - Argument1.X;
        var dy = Argument2.Y - Argument1.Y;
        var dz = Argument2.Z - Argument1.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public partial float DotProduct(Vec3 Argument1, Vec3 Argument2) =>
        Argument1.X * Argument2.X + Argument1.Y * Argument2.Y + Argument1.Z * Argument2.Z;

    public partial Vec3 CrossProduct(Vec3 Argument1, Vec3 Argument2) => new Vec3(
        Argument1.Y * Argument2.Z - Argument1.Z * Argument2.Y,
        Argument1.Z * Argument2.X - Argument1.X * Argument2.Z,
        Argument1.X * Argument2.Y - Argument1.Y * Argument2.X);

    public partial float Angle(Vec3 Argument1, Vec3 Argument2)
    {
        var dot = DotProduct(Argument1, Argument2);
        var magA = Distance(Argument1, default);
        var magB = Distance(Argument2, default);
        if (magA == 0f || magB == 0f) return 0f;
        var cosAngle = Math.Max(-1.0, Math.Min(1.0, dot / (magA * magB)));
        return (float)Math.Acos(cosAngle);
    }

    public partial float OrientedAngle(Vec3 Argument1, Vec3 Argument2)
    {
        var angle = Angle(Argument1, Argument2);
        var cross = CrossProduct(Argument1, Argument2);
        return cross.Z < 0f ? -angle : angle;
    }

    public partial float Angle(float _Radian1, float _Radian2)
    {
        var diff = _Radian2 - _Radian1;
        var twoPi = (float)(2.0 * Math.PI);
        diff = ((diff % twoPi) + twoPi) % twoPi;
        if (diff > (float)Math.PI) diff -= twoPi;
        return (float)Math.Abs(diff);
    }

    public partial float Angle(Vec2 Argument1, Vec2 Argument2)
    {
        var dot = Argument1.X * Argument2.X + Argument1.Y * Argument2.Y;
        var magA = (float)Math.Sqrt(Argument1.X * Argument1.X + Argument1.Y * Argument1.Y);
        var magB = (float)Math.Sqrt(Argument2.X * Argument2.X + Argument2.Y * Argument2.Y);
        if (magA == 0f || magB == 0f) return 0f;
        var cosAngle = Math.Max(-1.0, Math.Min(1.0, dot / (magA * magB)));
        return (float)Math.Acos(cosAngle);
    }

    public partial float OrientedAngle(Vec2 Argument1, Vec2 Argument2) =>
        (float)Math.Atan2(
            Argument1.X * Argument2.Y - Argument1.Y * Argument2.X,
            Argument1.X * Argument2.X + Argument1.Y * Argument2.Y);

    public partial int Max(int _A, int _B) => Math.Max(_A, _B);
    public partial int Min(int _A, int _B) => Math.Min(_A, _B);
    public partial int Clamp(int _X, int _Min, int _Max) =>
        _X < _Min ? _Min : _X > _Max ? _Max : _X;

    public partial float Max(float _A, float _B) => (float)Math.Max((double)_A, (double)_B);
    public partial float Min(float _A, float _B) => (float)Math.Min((double)_A, (double)_B);
    public partial float Clamp(float _X, float _Min, float _Max) =>
        _X < _Min ? _Min : _X > _Max ? _Max : _X;

    public partial float Mod(float _X, float _Min, float _Max)
    {
        var range = _Max - _Min;
        if (range == 0f) return _Min;
        return (((_X - _Min) % range) + range) % range + _Min;
    }
}
