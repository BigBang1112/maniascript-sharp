using System.Collections.Generic;
using Xunit;

namespace ManiaScriptSharp.ManiaPlanet.Tests;

public class PrimitiveEqualityTests
{
    [Fact]
    public void Vec2_UsesComponentValueEquality()
    {
        var value = new Vec2(1, 2);
        var equal = new Vec2(1, 2);

        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.True(value != new Vec2(2, 2));
        Assert.True(value != new Vec2(1, 3));
        Assert.True(value.Equals((object)equal));
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
    }

    [Fact]
    public void Vec3_UsesComponentValueEquality()
    {
        var value = new Vec3(1, 2, 3);
        var equal = new Vec3(1, 2, 3);

        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.True(value != new Vec3(2, 2, 3));
        Assert.True(value != new Vec3(1, 3, 3));
        Assert.True(value != new Vec3(1, 2, 4));
        Assert.True(value.Equals((object)equal));
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
    }

    [Fact]
    public void Int2_UsesComponentValueEquality()
    {
        var value = new Int2(1, 2);
        var equal = new Int2(1, 2);

        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.True(value != new Int2(2, 2));
        Assert.True(value != new Int2(1, 3));
        Assert.True(value.Equals((object)equal));
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
    }

    [Fact]
    public void Int3_UsesComponentValueEquality()
    {
        var value = new Int3(1, 2, 3);
        var equal = new Int3(1, 2, 3);

        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.True(value != new Int3(2, 2, 3));
        Assert.True(value != new Int3(1, 3, 3));
        Assert.True(value != new Int3(1, 2, 4));
        Assert.True(value.Equals((object)equal));
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
        Assert.Contains(equal, new HashSet<Int3> { value });
    }
}
