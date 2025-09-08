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

    public const int Ticks = 5;
    private const int MaxTicksPerAxis = 4;

    public List<Vector2> data = new List<Vector2>();
    public List<Vector2> scaledPoints = new List<Vector2>();

    // Visible time labels aligned with `data`
    private readonly List<string> visibleTimes = new List<string>();

    public Vector2 origin = new Vector2(Margin, Height + Margin);

    // ========= NEW: Stable Y axis with hysteresis =========
    private float axisYMin = float.NaN;
    private float axisYMax = float.NaN;

    // Hysteresis configuration
    // - yHystPct: percentage of current axis range used as hysteresis band
    // - yHystAbs: absolute minimum hysteresis size (overrides when larger)
    private float yHystPct = 0.10f;  // 10% of the current axis range
    private float yHystAbs = 0.0f;   // add a fixed buffer if desired (e.g., 2.0f)

    // How many Y ticks to try to show; the "nice" algorithm will choose the step
    private int yDesiredTicks = 5;

    // Cached ticks (rebuilt only when axis changes)
    private readonly List<float> yTickValues = new List<float>();
    private readonly List<string> yTickLabels = new List<string>();
    private bool yAxisChanged = true;
    // ======================================================

    public override void _PhysicsProcess(double delta)
    {
        time += delta * 1.0;

        if (count < (int)time)
        {
            if (count > dataNox.Count())
            {
                GD.Print("no more data");
                return; // prevent out-of-range access
            }

            count++;

            // Maintain sliding window and keep time labels in sync
            if (data.Count() < dataPointsMax - 1)
            {
                data.Add(new Vector2((float)count * 5, dataNox[count]));
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

        // ---------- X axis ticks (existing behavior; distributed across visible points) ----------
        if (scaledPoints.Count > 0)
        {
            var tickIdx = GetDistributedIndices(scaledPoints.Count, MaxTicksPerAxis);
            int fontSize = 14;
            Color labelColor = Colors.LightGray;

            // X-axis ticks & labels (under selected visible points)
            foreach (int i in tickIdx)
            {
                float xScreen = Margin + scaledPoints[i].X;
                // Tick
                DrawLine(new Vector2(xScreen, origin.Y - 5), new Vector2(xScreen, origin.Y + 5), Colors.Gray);
                // Label (time if available)
                string xLabel = FormatTimeLabel(SafeGet(visibleTimes, i));
                if (string.IsNullOrEmpty(xLabel))
                {
                    xLabel = SafeGet(data, i).X.ToString("0");
                }
                Vector2 xSize = defaultfont.GetStringSize(xLabel, fontSize);
                Vector2 xPos = new Vector2(xScreen - xSize.X / 2f, origin.Y + 20f);
                DrawString(defaultfont, xPos, xLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
            }
        }

        // ---------- NEW: Y axis ticks from cached "nice" ticks ----------
        if (yTickValues.Count > 0 && !float.IsNaN(axisYMin) && !float.IsNaN(axisYMax) && axisYMax > axisYMin)
        {
            int fontSize = 14;
            Color labelColor = Colors.LightGray;

            foreach (var tickVal in yTickValues)
            {
                float yScreen = YValueToScreen(tickVal); // position inside plot area

                // Tick line
                DrawLine(new Vector2(Margin - 5, yScreen), new Vector2(Margin + 5, yScreen), Colors.Gray);

                // Label (right-aligned to the left of axis)
                string yLabel = tickVal.ToString("0.###", CultureInfo.InvariantCulture);
                Vector2 ySize = defaultfont.GetStringSize(yLabel, fontSize);
                Vector2 yPos = new Vector2(Margin - 10f - ySize.X, yScreen + ySize.Y * 0.35f);
                DrawString(defaultfont, yPos, yLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
            }
        }

        // ---------- Plot line ----------
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
        // --- Compute current window ranges (X dynamic, Y data min/max) ---
        float xMax = float.MinValue, xMin = float.MaxValue;
        float yDataMax = float.MinValue, yDataMin = float.MaxValue;

        foreach (var point in data)
        {
            xMax = Math.Max(xMax, point.X);
            xMin = Math.Min(xMin, point.X);
            yDataMax = Math.Max(yDataMax, point.Y);
            yDataMin = Math.Min(yDataMin, point.Y);
        }

        float xRange = xMax - xMin == 0 ? 1 : xMax - xMin;

        // --- Update persistent Y axis with hysteresis ---
        if (UpdateYAxisRange(yDataMin, yDataMax))
        {
            // Rebuild "nice" ticks and snap the axis to nice min/max
            RebuildYTicks();
        }

        // Safety: ensure axis is usable
        if (float.IsNaN(axisYMin) || float.IsNaN(axisYMax) || axisYMax <= axisYMin)
        {
            // Initialize to data range if not yet available
            axisYMin = yDataMin;
            axisYMax = yDataMax;
            if (axisYMax - axisYMin < 1e-6f)
            {
                axisYMin -= 0.5f;
                axisYMax += 0.5f;
            }
            RebuildYTicks();
        }

        float yAxisRange = axisYMax - axisYMin;
        if (yAxisRange < 1e-6f) yAxisRange = 1.0f; // avoid division by zero

        // --- Scale points using persistent axis for Y ---
        scaledPoints.Clear();
        foreach (var point in data)
        {
            float scaledX = ((point.X - xMin) / xRange) * Width;
            float scaledY = Height - ((point.Y - axisYMin) / yAxisRange) * Height;

            // Optional: avoid pinning to exact borders visually
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

        // Reflect computed ticks into legacy field if you still use it elsewhere
        yticks = yTickValues.ToArray();

        QueueRedraw();
    }

    // ========= NEW: Y axis hysteresis management =========

    /// <summary>
    /// Update axisYMin/axisYMax only when data min/max crosses a hysteresis band.
    /// Returns true if the axis range changed.
    /// </summary>
    private bool UpdateYAxisRange(float yDataMin, float yDataMax)
    {
        // First-time init
        if (float.IsNaN(axisYMin) || float.IsNaN(axisYMax))
        {
            axisYMin = yDataMin;
            axisYMax = yDataMax;
            // Ensure non-zero span
            if (axisYMax - axisYMin < 1e-6f)
            {
                axisYMin -= 0.5f;
                axisYMax += 0.5f;
            }
            yAxisChanged = true;
            return true;
        }

        float currentRange = Math.Max(1e-6f, axisYMax - axisYMin);
        float hysteresis = Math.Max(yHystAbs, yHystPct * currentRange);

        bool changed = false;

        // Expand/shrink only when crossing the band (Schmitt trigger style)
        if (yDataMin < axisYMin - hysteresis) { axisYMin = yDataMin; changed = true; }
        else if (yDataMin > axisYMin + hysteresis) { axisYMin = yDataMin; changed = true; }

        if (yDataMax > axisYMax + hysteresis) { axisYMax = yDataMax; changed = true; }
        else if (yDataMax < axisYMax - hysteresis) { axisYMax = yDataMax; changed = true; }

        if (changed)
        {
            // Keep a minimum range
            if (axisYMax - axisYMin < 1e-6f)
            {
                axisYMin -= 0.5f;
                axisYMax += 0.5f;
            }
        }

        yAxisChanged = changed;
        return changed;
    }

    /// <summary>
    /// Rebuild cached Y ticks using "nice" numbers and snap the axis to nice min/max.
    /// </summary>
    private void RebuildYTicks()
    {
        yTickValues.Clear();
        yTickLabels.Clear();

        float min = axisYMin;
        float max = axisYMax;
        if (max <= min)
        {
            max = min + 1f;
        }

        // Compute nice axis
        ComputeNiceAxis(min, max, yDesiredTicks, out float niceMin, out float niceMax, out float tickStep, out int tickCount);

        // Snap axis to nice bounds so scaling matches tick positions
        axisYMin = niceMin;
        axisYMax = niceMax;

        // Build ticks
        for (int i = 0; i < tickCount; i++)
        {
            float v = niceMin + i * tickStep;
            // Avoid FP noise
            v = (float)Math.Round(v, 6, MidpointRounding.AwayFromZero);
            yTickValues.Add(v);
            yTickLabels.Add(v.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    private static float NiceNum(float range, bool round)
    {
        // Returns a "nice" number approximately equal to range.
        // Rounds the number if round=true; otherwise takes ceiling.
        double exponent = Math.Floor(Math.Log10(Math.Max(1e-12, range)));
        double fraction = range / Math.Pow(10, exponent);
        double niceFraction;

        if (round)
        {
            if (fraction < 1.5) niceFraction = 1;
            else if (fraction < 3) niceFraction = 2;
            else if (fraction < 7) niceFraction = 5;
            else niceFraction = 10;
        }
        else
        {
            if (fraction <= 1) niceFraction = 1;
            else if (fraction <= 2) niceFraction = 2;
            else if (fraction <= 5) niceFraction = 5;
            else niceFraction = 10;
        }

        return (float)(niceFraction * Math.Pow(10, exponent));
    }

    private static void ComputeNiceAxis(float min, float max, int targetTicks,
                                        out float niceMin, out float niceMax,
                                        out float tickSpacing, out int tickCount)
    {
        float rawRange = Math.Max(1e-6f, max - min);
        float niceRange = NiceNum(rawRange, false);
        tickSpacing = NiceNum(niceRange / Math.Max(2, targetTicks - 1), true);

        niceMin = (float)Math.Floor(min / tickSpacing) * tickSpacing;
        niceMax = (float)Math.Ceiling(max / tickSpacing) * tickSpacing;

        tickCount = (int)Math.Round((niceMax - niceMin) / tickSpacing) + 1;
        // Keep within reasonable bounds
        if (tickCount > targetTicks + 2)
        {
            // Reduce if too many
            float span = niceMax - niceMin;
            tickSpacing = NiceNum(span / targetTicks, true);
            niceMin = (float)Math.Floor(min / tickSpacing) * tickSpacing;
            niceMax = (float)Math.Ceiling(max / tickSpacing) * tickSpacing;
            tickCount = (int)Math.Round((niceMax - niceMin) / tickSpacing) + 1;
        }
        if (tickCount < 2)
        {
            tickCount = 2;
            tickSpacing = Math.Max(1e-3f, (niceMax - niceMin) / (tickCount - 1));
        }
    }

    private float YValueToScreen(float yVal)
    {
        float range = Math.Max(1e-6f, axisYMax - axisYMin);
        float t = (yVal - axisYMin) / range;           // 0 at min, 1 at max
        float yPlot = Height - t * Height;             // 0 at bottom -> flip to top-left coords
        return Margin + yPlot;                         // add top margin
    }

    // --------------------- Helpers (existing) ---------------------

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
