using wledBridge.VirtualMixer.Models;
using Xunit;

namespace wledBridge.VirtualMixer.Tests;

public class CurveAndTransformTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 64)]
    [InlineData(1.0, 127)]
    public void Linear_curve_maps_proportionally(double position, int expected)
    {
        var curve = new Curve { Type = CurveType.Linear };
        Assert.Equal(expected, curve.Apply(position));
    }

    [Fact]
    public void Exponential_curve_is_below_linear_in_the_middle()
    {
        var curve = new Curve { Type = CurveType.Exponential, Gamma = 2.0 };
        Assert.Equal(0, curve.Apply(0));
        Assert.Equal(127, curve.Apply(1));
        Assert.True(curve.Apply(0.5) < 64);
    }

    [Fact]
    public void Logarithmic_curve_is_above_linear_in_the_middle()
    {
        var curve = new Curve { Type = CurveType.Logarithmic, Gamma = 2.0 };
        Assert.Equal(0, curve.Apply(0));
        Assert.Equal(127, curve.Apply(1));
        Assert.True(curve.Apply(0.5) > 64);
    }

    [Theory]
    [InlineData(CurveType.Linear)]
    [InlineData(CurveType.Exponential)]
    [InlineData(CurveType.Logarithmic)]
    public void ToPosition_inverts_Apply(CurveType type)
    {
        var curve = new Curve { Type = type, Gamma = 2.5 };
        foreach (var value in new[] { 0, 1, 42, 64, 100, 127 })
        {
            Assert.Equal(value, curve.Apply(curve.ToPosition(value)));
        }
    }

    [Fact]
    public void Transform_invert()
    {
        var t = new BindingTransform { Type = BindingTransformType.Invert };
        Assert.Equal(127, t.Apply(0));
        Assert.Equal(0, t.Apply(127));
        Assert.Equal(63, t.Apply(64));
    }

    [Fact]
    public void Transform_rangemap_clamps_and_maps()
    {
        var t = new BindingTransform
        {
            Type = BindingTransformType.RangeMap,
            InMin = 0, InMax = 127, OutMin = 0, OutMax = 64
        };
        Assert.Equal(0, t.Apply(0));
        Assert.Equal(64, t.Apply(127));
        Assert.Equal(32, t.Apply(64));
    }

    [Fact]
    public void Transform_threshold()
    {
        var t = new BindingTransform { Type = BindingTransformType.Threshold, Threshold = 100 };
        Assert.Equal(0, t.Apply(99));
        Assert.Equal(127, t.Apply(100));
    }
}
