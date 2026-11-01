using System.Globalization;
using System.Text;

namespace SkySaga.Admin.Components.Charts;

public static class ChartGeometry
{
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static double NiceMax(double max)
    {
        if (max <= 0)
            return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(max)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(f => f * magnitude).First(s => s * 4 >= max);

        return step * 4;
    }

    public static string SmoothPath(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count == 0)
            return "";

        var path = new StringBuilder($"M{Number(points[0].X)},{Number(points[0].Y)}");

        if (points.Count == 1)
            return path.ToString();

        var count = points.Count;
        var slopes = new double[count - 1];
        var tangents = new double[count];

        for (var i = 0; i < count - 1; i++)
            slopes[i] = (points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X);

        tangents[0] = slopes[0];
        tangents[count - 1] = slopes[count - 2];

        for (var i = 1; i < count - 1; i++)
            tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;

        for (var i = 0; i < count - 1; i++)
        {
            if (slopes[i] == 0)
            {
                tangents[i] = 0;
                tangents[i + 1] = 0;
                continue;
            }

            var a = tangents[i] / slopes[i];
            var b = tangents[i + 1] / slopes[i];
            var length = a * a + b * b;

            if (length > 9)
            {
                var scale = 3 / Math.Sqrt(length);
                tangents[i] = scale * a * slopes[i];
                tangents[i + 1] = scale * b * slopes[i];
            }
        }

        for (var i = 0; i < count - 1; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[i + 1];
            var third = (x1 - x0) / 3;

            path.Append($" C{Number(x0 + third)},{Number(y0 + tangents[i] * third)} {Number(x1 - third)},{Number(y1 - tangents[i + 1] * third)} {Number(x1)},{Number(y1)}");
        }

        return path.ToString();
    }

    public static string NewId() => "g" + Guid.NewGuid().ToString("N")[..10];
}
