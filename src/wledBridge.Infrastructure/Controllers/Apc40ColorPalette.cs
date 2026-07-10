using wledBridge.Application.Abstractions.Controllers;

namespace wledBridge.Infrastructure.Controllers;

internal static class Apc40ColorPalette
{
    private static readonly Dictionary<byte, ControllerColor> VelocityToColor = new()
    {
        [0] = new ControllerColor(0, 0, 0),
        [1] = new ControllerColor(255, 255, 255),
        [2] = new ControllerColor(128, 128, 128),
        [5] = new ControllerColor(255, 0, 0),
        [6] = new ControllerColor(120, 0, 0),
        [9] = new ControllerColor(255, 140, 0),
        [10] = new ControllerColor(120, 70, 0),
        [13] = new ControllerColor(255, 255, 0),
        [14] = new ControllerColor(120, 120, 0),
        [17] = new ControllerColor(140, 255, 0),
        [18] = new ControllerColor(70, 120, 0),
        [21] = new ControllerColor(0, 255, 0),
        [22] = new ControllerColor(0, 120, 0),
        [25] = new ControllerColor(0, 255, 140),
        [26] = new ControllerColor(0, 120, 70),
        [29] = new ControllerColor(0, 255, 255),
        [30] = new ControllerColor(0, 120, 120),
        [33] = new ControllerColor(0, 140, 255),
        [34] = new ControllerColor(0, 70, 120),
        [37] = new ControllerColor(0, 0, 255),
        [38] = new ControllerColor(0, 0, 120),
        [41] = new ControllerColor(140, 0, 255),
        [42] = new ControllerColor(70, 0, 120),
        [45] = new ControllerColor(255, 0, 255),
        [46] = new ControllerColor(120, 0, 120),
        [49] = new ControllerColor(255, 0, 140),
        [50] = new ControllerColor(120, 0, 70),
    };

    public static byte FindNearestVelocity(ControllerColor color)
    {
        if (color is { R: 0, G: 0, B: 0 })
        {
            return 0;
        }

        var best = (byte)1;
        var bestDistance = double.MaxValue;

        foreach (var (velocity, candidate) in VelocityToColor)
        {
            var dr = candidate.R - color.R;
            var dg = candidate.G - color.G;
            var db = candidate.B - color.B;
            var distance = (dr * dr) + (dg * dg) + (db * db);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = velocity;
            }
        }

        return best;
    }
}
