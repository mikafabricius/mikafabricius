using Godot;
using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

public partial class Graph : Control
{
	public float[] dataNox = new float[] { };
	public string[] dataTime = new string[] { };
	public static int count = 0;
	private double time;
	public Font defaultfont = ThemeDB.FallbackFont;
	public const int dataPointsMax = 36;
	public List<Vector2> data = new List<Vector2>();
	public List<Vector2> scaledPoints = new List<Vector2>();
	private readonly List<string> visibleTimes = new List<string>();
	public Vector2 origin = new Vector2(100, 600);
	private const int Width = 800;
	private const int Height = 500;
	private const int Margin = 100;
	private float axisYMin = float.NaN, axisYMax = float.NaN;
	private float yHystPct = 0.10f;
	private float yHystAbs = 0f;
	private int yDesiredTicks = 5;
	private readonly List<float> yTickValues = new List<float>();

	public override void _PhysicsProcess(double delta)
	{
		time += delta;
		if (count < (int)time)
		{
			if (count > dataNox.Length - 1) return;
			count++;
			if (data.Count < dataPointsMax - 1)
			{
				data.Add(new Vector2(count * 5, dataNox[count]));
				visibleTimes.Add(count < dataTime.Length ? dataTime[count] : "");
			}
			else
			{
				data.RemoveAt(0);
				if (visibleTimes.Count > 0) visibleTimes.RemoveAt(0);
				data.Add(new Vector2(count * 5, dataNox[count]));
				visibleTimes.Add(count < dataTime.Length ? dataTime[count] : "");
			}
			Plot(data);
		}
	}

	public override void _Ready()
	{
		ReadData();
	}

	public override void _Draw()
	{
		// TODO make envelope with the min and max from the miljøstyrelsen
		int fontSize = 14;
		Color labelColor = Colors.LightGray;
		Color axisColor = Colors.Gray;
		Color tickColor = Colors.Gray;
		DrawLine(origin, origin + new Vector2(Width, 0), axisColor, 2);
		DrawLine(origin, origin - new Vector2(0, Height), axisColor, 2);
		if (scaledPoints.Count > 0)
		{
			var tickIdx = GetDistributedIndices(scaledPoints.Count, 4);
			foreach (int i in tickIdx)
			{
				float xScreen = Margin + scaledPoints[i].X;
				DrawLine(new Vector2(xScreen, origin.Y - 5), new Vector2(xScreen, origin.Y + 5), tickColor);
				string xLabel = FormatTimeLabel(SafeGet(visibleTimes, i));
				if (string.IsNullOrEmpty(xLabel)) xLabel = SafeGet(data, i).X.ToString("0");
				Vector2 xSize = defaultfont.GetStringSize(text: xLabel, fontSize: fontSize);
				Vector2 xPos = new Vector2(xScreen - xSize.X / 2f, origin.Y + 20f);
				DrawString(defaultfont, xPos, xLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
			}
		}

		if (yTickValues.Count > 0 && !float.IsNaN(axisYMin) && !float.IsNaN(axisYMax) && axisYMax > axisYMin)
		{
			foreach (var tickVal in yTickValues)
			{
				float yScreen = YValueToScreen(tickVal);
				DrawLine(new Vector2(Margin - 5, yScreen), new Vector2(Margin + 5, yScreen), tickColor);
				string yLabel = tickVal.ToString("0.###", CultureInfo.InvariantCulture);
				Vector2 ySize = defaultfont.GetStringSize(text: yLabel, fontSize: fontSize);
				Vector2 yPos = new Vector2(Margin - 10f - ySize.X, yScreen + ySize.Y * 0.35f);
				DrawString(defaultfont, yPos, yLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
			}
		}

		for (int i = 0; i < scaledPoints.Count - 1; i++)
			DrawLine(new Vector2(Margin, Margin) + scaledPoints[i], new Vector2(Margin, Margin) + scaledPoints[i + 1], Colors.Green, 2, true);

		Vector2 xLabelSize = defaultfont.GetStringSize("Time [HH.MM.SS]");
		Vector2 xLabelPos = new Vector2(origin.X + Width/2 - xLabelSize.X/2, origin.Y + Margin/2);
		DrawString(defaultfont, xLabelPos, "Time [HH.MM.SS]", HorizontalAlignment.Left, -1, fontSize, labelColor);
		Vector2 yLabelSize = defaultfont.GetStringSize("nox emmision [mg/Nm3]", fontSize: fontSize);
		Vector2 yLabelPos = new Vector2(origin.X - Margin/2, origin.Y - Height/2 + yLabelSize.X/2);
		DrawSetTransformMatrix(new Transform2D(Mathf.DegToRad(-90), yLabelPos));
		DrawString(defaultfont, Vector2.Zero, "nox emmision [mg/nm3]", HorizontalAlignment.Left, -1, fontSize, labelColor);
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
				if (!firstLine) firstLine = true;
				else
				{
					dataTime = dataTime.Append(values[0]).ToArray();
					dataNox = dataNox.Append(float.Parse(values[5], CultureInfo.InvariantCulture.NumberFormat)).ToArray();
				}
			}
		}
	}

	public void Plot(List<Vector2> data)
	{
		float xMax = float.MinValue, xMin = float.MaxValue, yDataMax = float.MinValue, yDataMin = float.MaxValue;
		foreach (var point in data)
		{
			xMax = Math.Max(xMax, point.X);
			xMin = Math.Min(xMin, point.X);
			yDataMax = Math.Max(yDataMax, point.Y);
			yDataMin = Math.Min(yDataMin, point.Y);
		}
		float xRange = xMax - xMin == 0 ? 1 : xMax - xMin;
		if (UpdateYAxisRange(yDataMin, yDataMax)) RebuildYTicks();
		if (float.IsNaN(axisYMin) || float.IsNaN(axisYMax) || axisYMax <= axisYMin)
		{
			axisYMin = yDataMin;
			axisYMax = yDataMax;
			if (axisYMax - axisYMin < 1e-6f) { axisYMin -= 0.5f; axisYMax += 0.5f; }
			RebuildYTicks();
		}
		float yAxisRange = axisYMax - axisYMin;
		if (yAxisRange < 1e-6f) yAxisRange = 1.0f;
		scaledPoints.Clear();
		foreach (var point in data)
		{
			float scaledX = ((point.X - xMin) / xRange) * Width;
			float scaledY = Height - ((point.Y - axisYMin) / yAxisRange) * Height;
			scaledY = Math.Max(0, Math.Min(Height, scaledY));
			if (scaledY == 0) scaledY = Height * 0.05f;
			else if (scaledY == Height) scaledY = Height * 0.95f;
			if (scaledPoints.Count < dataPointsMax - 1) scaledPoints.Add(new Vector2(scaledX, scaledY));
			else { scaledPoints.RemoveAt(0); scaledPoints.Add(new Vector2(scaledX, scaledY)); }
		}
		QueueRedraw();
	}

	private bool UpdateYAxisRange(float yDataMin, float yDataMax)
	{
		if (float.IsNaN(axisYMin) || float.IsNaN(axisYMax))
		{
			axisYMin = yDataMin;
			axisYMax = yDataMax;
			if (axisYMax - axisYMin < 1e-6f) { axisYMin -= 0.5f; axisYMax += 0.5f; }
			return true;
		}
		float currentRange = Math.Max(1e-6f, axisYMax - axisYMin);
		float hysteresis = Math.Max(yHystAbs, yHystPct * currentRange);
		bool changed = false;
		if (yDataMin < axisYMin - hysteresis) { axisYMin = yDataMin; changed = true; }
		else if (yDataMin > axisYMin + hysteresis) { axisYMin = yDataMin; changed = true; }
		if (yDataMax > axisYMax + hysteresis) { axisYMax = yDataMax; changed = true; }
		else if (yDataMax < axisYMax - hysteresis) { axisYMax = yDataMax; changed = true; }
		if (changed && axisYMax - axisYMin < 1e-6f) { axisYMin -= 0.5f; axisYMax += 0.5f; }
		return changed;
	}

	private void RebuildYTicks()
	{
		yTickValues.Clear();
		float min = axisYMin, max = axisYMax;
		if (max <= min) max = min + 1f;
		ComputeNiceAxis(min, max, yDesiredTicks, out float niceMin, out float niceMax, out float tickStep, out int tickCount);
		axisYMin = niceMin;
		axisYMax = niceMax;
		for (int i = 0; i < tickCount; i++)
			yTickValues.Add((float)Math.Round(niceMin + i * tickStep, 6, MidpointRounding.AwayFromZero));
	}

	private static float NiceNum(float range, bool round)
	{
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

	private static void ComputeNiceAxis(float min, float max, int targetTicks, out float niceMin, out float niceMax, out float tickSpacing, out int tickCount)
	{
		float rawRange = Math.Max(1e-6f, max - min);
		float niceRange = NiceNum(rawRange, false);
		tickSpacing = NiceNum(niceRange / Math.Max(2, targetTicks - 1), true);
		niceMin = (float)Math.Floor(min / tickSpacing) * tickSpacing;
		niceMax = (float)Math.Ceiling(max / tickSpacing) * tickSpacing;
		tickCount = (int)Math.Round((niceMax - niceMin) / tickSpacing) + 1;
		if (tickCount > targetTicks + 2)
		{
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
		float t = (yVal - axisYMin) / range;
		float yPlot = Height - t * Height;
		yPlot = Math.Max(0, Math.Min(Height, yPlot));
		return Margin + yPlot;
	}

	private static List<int> GetDistributedIndices(int count, int maxTicks)
	{
		var indices = new List<int>();
		if (count <= 0) return indices;
		int ticks = Math.Min(maxTicks, count);
		if (ticks == 1) { indices.Add(count - 1); return indices; }
		for (int i = 0; i < ticks; i++)
		{
			int idx = (int)Math.Round(i * (count - 1) / (float)(ticks - 1));
			if (indices.Count == 0 || idx != indices[^1]) indices.Add(idx);
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
