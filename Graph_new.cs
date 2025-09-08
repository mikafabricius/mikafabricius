using Godot;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;

// [Tool]
public partial class Graph : Control
{
    public float[] xticks, yticks;
    public string[] dataTime = new string[] { };
    public float[] dataNox = new float[] { };
    public static int count = 0;
    private double time;
    public Rect2 viewport;
    public Font defaultfont = ThemeDB.FallbackFont;

    public const int dataPointsMax = 36;
    public float[] dataX = new float[dataPointsMax];
    public float[] dataY = new float[dataPointsMax];

    private const int Width = 800;
    private const int Height = 500;
    private const int Margin = 100;

    // CHANGED: no longer used for modulo stepping; we’ll keep but not rely on it
    public const int Ticks = 5;

    // NEW: maximum ticks per axis
    private const int MaxTicksPerAxis = 4;

    public List<Vector2> data = new List<Vector2>();
    public List<Vector2> scaledPoints = new List<Vector2>();

    // NEW: visible time labels aligned 1:1 with `data`
    private readonly List<string> visibleTimes = new List<string>();

    public Vector2 origin = new Vector2(Margin, Height + Margin);

    public override void _PhysicsProcess(double delta)
    {
        time += delta * 1.0;
        if (count < (int)time)
        {
            if (count > dataNox.Count())
            {
                GD.Print("no more data");
                return; // NEW: prevent out-of-range access
            }

            count++;

            // Add/remove to keep a sliding window and keep time labels in sync
            if (data.Count() < dataPointsMax - 1)
            {
                data.Add(new Vector2((float)count * 5, dataNox[count]));
                // NEW: push time label for this point
                if (count < dataTime.Length) visibleTimes.Add(dataTime[count]);
                else visibleTimes.Add(string.Empty);
            }
            else
            {
                data.RemoveAt(0);
                if (visibleTimes.Count > 0) visibleTimes.RemoveAt(0);

                data.Add(new Vector2((float)count * 5, dataNox[count]));
                if (count < dataTime.Length) visibleTimes.Add(dataTime[count]);
                else visibleTimes.Add(string.Empty);
            }

            Plot(data);
        }
    }

    public override void _Ready()
    {
        GD.Print("hello");
        ReadData();
    }

    public override void _Draw()
    {
        // Axes
        DrawLine(origin, origin + new Vector2(Width, 0), Colors.Gray, 2);
        DrawLine(origin, origin - new Vector2(0, Height), Colors.Gray, 2);

        // --- NEW: sliding ticks on X and Y based on visible points ---
        if (scaledPoints.Count > 0)
        {
            var tickIdx = GetDistributedIndices(scaledPoints.Count, MaxTicksPerAxis);

            // Fonts / sizes
            int fontSize = 14;
            Color labelColor = Colors.LightGray;

            // Draw X-axis ticks & labels (under selected visible points)
            foreach (int i in tickIdx)
            {
                float xScreen = Margin + scaledPoints[i].X;

                // Tick
                DrawLine(new Vector2(xScreen, origin.Y - 5), new Vector2(xScreen, origin.Y + 5), Colors.Gray);

                // Label (time if available)
                string xLabel = FormatTimeLabel(SafeGet(visibleTimes, i));
                if (string.IsNullOrEmpty(xLabel))
                {
                    // fallback to raw X value
                    xLabel = SafeGet(data, i).X.ToString("0");
                }

                Vector2 xSize = defaultfont.GetStringSize(xLabel, fontSize);
                // Slight padding below axis
                Vector2 xPos = new Vector2(xScreen - xSize.X / 2f, origin.Y + 20f);
                DrawString(defaultfont, xPos, xLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
            }

            // Draw Y-axis ticks & labels (left of axis at the same selected indices)
            foreach (int i in tickIdx)
            {
                float yScreen = Margin + scaledPoints[i].Y;

                // Tick
                DrawLine(new Vector2(Margin - 5, yScreen), new Vector2(Margin + 5, yScreen), Colors.Gray);

                // Label from actual point.Y (not scaled)
                float yVal = SafeGet(data, i).Y;
                string yLabel = yVal.ToString("0.###");
                Vector2 ySize = defaultfont.GetStringSize(yLabel, fontSize);
                // Right-align the label to the left of the axis, vertically centered on tick
                Vector2 yPos = new Vector2(Margin - 10f - ySize.X, yScreen + ySize.Y * 0.35f);
                DrawString(defaultfont, yPos, yLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
            }
        }

        // --- Existing line drawing (unchanged except we removed modulo-tick logic) ---
        for (int i = 0; i < scaledPoints.Count() - 1; i++)
        {
            DrawLine(new Vector2(Margin, Margin) + scaledPoints[i],
                     new Vector2(Margin, Margin) + scaledPoints[i + 1],
                     Colors.Green, 2, true);
        }
    }

    public void ReadData()
    {
        using (StreamReader reader = new StreamReader("data/complete_measurements_with_designations_20240520_000000_20240603_000000.txt"))
        {
            bool firstLine = false;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');
                if (!firstLine)
                {
                    firstLine = true;
                }
                else
                {
                    dataTime = dataTime.Append<string>((string)values[0]).ToArray();
                    dataNox = dataNox.Append<float>(float.Parse(values[5], CultureInfo.InvariantCulture.NumberFormat)).ToArray();
                }
            }
        }
    }

    public void Plot(List<Vector2> data)
    {
        float xMax = float.MinValue, xMin = float.MaxValue;
        float yMax = float.MinValue, yMin = float.MaxValue;
        foreach (var point in data)
        {
            xMax = Math.Max(xMax, point.X);
            xMin = Math.Min(xMin, point.X);
            yMax = Math.Max(yMax, point.Y);
            yMin = Math.Min(yMin, point.Y);
        }
        float xRange = xMax - xMin == 0 ? 1 : xMax - xMin;
        float yRange = yMax - yMin == 0 ? 1 : yMax - yMin;

        scaledPoints.Clear();
        foreach (var point in data)
        {
            float scaledX = ((point.X - xMin) / xRange) * Width;
            float scaledY = Height - ((point.Y - yMin) / yRange) * Height;

            // Avoid pinning to exact borders
            if (scaledY == 0) scaledY = Height * 0.05f;
            else if (scaledY == Height) scaledY = Height * 0.95f;

            if (scaledPoints.Count() < dataPointsMax - 1)
                scaledPoints.Add(new Vector2(scaledX, scaledY));
            else
            {
                scaledPoints.RemoveAt(0);
                scaledPoints.Add(new Vector2(scaledX, scaledY));
            }
        }
        QueueRedraw();
    }

    public void FIFO(float[] array, int arraySize, float newValue)
    {
        float tmp1 = 0.0f;
        float tmp2 = 0.0f;
        for (int i = arraySize - 1; i > 0; i = i - 1)
        {
            if (i == arraySize - 1)
            {
                tmp1 = array[i - 1];
                array[i - 1] = array[i];
            }
            else
            {
                tmp2 = array[i - 1];
                array[i - 1] = tmp1;
                tmp1 = tmp2;
            }
        }
        array[arraySize - 1] = newValue;
        return;
    }

    // -----------------------
    // Helpers (NEW)
    // -----------------------

    // Pick up to `maxTicks` evenly distributed indices across [0..count-1]
    private static List<int> GetDistributedIndices(int count, int maxTicks)
    {
        var indices = new List<int>();
        if (count <= 0) return indices;

        int ticks = Math.Min(maxTicks, count);
        if (ticks == 1)
        {
            indices.Add(count - 1);
            return indices;
        }

        for (int i = 0; i < ticks; i++)
        {
            int idx = (int)Math.Round(i * (count - 1) / (float)(ticks - 1));
            if (indices.Count == 0 || idx != indices[^1])
                indices.Add(idx);
        }
        return indices;
    }

    private static string FormatTimeLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Try to parse and show HH:mm:ss; fall back to the raw string
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt.ToString("HH:mm:ss");

        return raw;
    }

    private static T SafeGet<T>(IList<T> list, int idx)
    {
        if (list == null || idx < 0 || idx >= list.Count) return default!;
        return list[idx];
    }
}
