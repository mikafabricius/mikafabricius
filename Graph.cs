using Godot;
using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
 

public partial class Graph : Control
{
	public float[] dataNox = new float[] { };
	public float[] dataOvenTemp = new float[] { };
	public float[] dataT2S = new float[] { };
	public float[] dataEBKTemp = new float[] { };
	public float[] dataNoxCalc = new float[] { };
	public float[] dataNoxSNCRFlow = new float[] { };
	public float[] dataNH3 = new float[] { };
	public float[] dataSmoke02 = new float[] { };
	public float[] dataMainFume = new float[] { };
	public float[] dataNM3Flow = new float[] { };
	public float[] dataSmokeFlow = new float[] { };
	public float[] dataPrimaryReg = new float[] { };
	public float[] dataPrimaryAirTemp = new float[] { };
	public float[] dataSecondaryOvenAir = new float[] { };
	public float[] dataSecondaryFlow = new float[] { };
	public float[] scalarMean = new float[] { };
	public float[] scalarStd = new float[] { };
	public string[] dataTime = new string[] { };
	private InferenceSession session;
	public static int count = 0;
	private double time;
	public Font defaultfont = ThemeDB.FallbackFont;
 
	// How many points you want to keep visible in the sliding window.
	public const int dataPointsMax = 36;
 
	// Visible ring buffer (last N points)
	public List<Vector2> data = new List<Vector2>();
	public List<Vector2> scaledPoints = new List<Vector2>();
	private readonly List<string> visibleTimes = new List<string>();
	public float upperLimit = 120.0f;
	public Rect2 envelopeNOX;
	public Color envelopeColor = new Color(0.862745f, 0.0784314f, 0.235294f, 0.3f);
 
	// Track the global integer index for each visible sample (aligned with 'data' and 'scaledPoints')
	private readonly List<int> dataGlobalIdx = new List<int>();
 
	// Persist which global indices should show an x-label (labels “follow” their points)
	private readonly List<int> xLabelGlobalIdx = new List<int>();
 
	public Vector2 origin = new Vector2(100, 600);
	private const int Width = 800;
	private const int Height = 500;
	private const int Margin = 100;
 
	// Y axis state
	private float axisYMin = float.NaN, axisYMax = float.NaN;
	private float yHystPct = 0.10f;
	private float yHystAbs = 0f;
	private int yDesiredTicks = 5;
	private readonly List<float> yTickValues = new List<float>();
 
	// Desired number of x labels across the plot width (first at left, then equally spaced)
	private int xDesiredTicks = 8;
 
	public override void _PhysicsProcess(double delta)
	{
		time += 2 * delta;
		if (count < (int)time)
		{
			if (count > dataNox.Length - 1) return;
			count++;
 
			// Append new sample (global index = 'count')
			if (data.Count < dataPointsMax)
			{
				data.Add(new Vector2(count * 5, dataNox[count]));
				visibleTimes.Add(count < dataTime.Length ? dataTime[count] : "");
				dataGlobalIdx.Add(count);
			}
			else
			{
				// FIFO
				data.RemoveAt(0);
				if (visibleTimes.Count > 0) visibleTimes.RemoveAt(0);
				if (dataGlobalIdx.Count > 0) dataGlobalIdx.RemoveAt(0);
 
				data.Add(new Vector2(count * 5, dataNox[count]));
				visibleTimes.Add(count < dataTime.Length ? dataTime[count] : "");
				dataGlobalIdx.Add(count);
			}
 
			Plot(data);

			// Predict using data
			float[] rawInput = new float[] {dataOvenTemp[count], dataT2S[count],
									dataEBKTemp[count], dataNoxCalc[count], dataNox[count],
									dataNoxSNCRFlow[count], dataNH3[count], dataSmoke02[count],
									dataMainFume[count], dataNM3Flow[count], dataSmokeFlow[count],
									dataPrimaryReg[count], dataPrimaryAirTemp[count], dataSecondaryOvenAir[count],
									dataSecondaryFlow[count]};

			float[] input = new float[rawInput.Length];
			for (int i = 0; i < rawInput.Length; i++)
			{
				input[i] = (rawInput[i] - scalarMean[i]) / scalarStd[i];
			}
			var shape = new int[] {1,1,15};
			var inputTensor = new DenseTensor<float>(input, shape);

			var inputName = session.InputMetadata.Keys.First();
			var outputName = session.OutputMetadata.Keys.First();

			var inputs = new List<NamedOnnxValue>
			{
				NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
			};

			using var results = session.Run(inputs);
			var prediction = results.First().AsEnumerable<float>().ToArray();
			GD.Print($"Predicted next value: {prediction[0]*scalarStd[4] + scalarMean[4]}");

		}
	}
 
	public override void _Ready()
	{
		string energnistData = "data/complete_measurements_with_designations_20240520_000000_20240603_000000.txt";
		dataTime = GetEnergnistData(energnistData, 0, true);
		dataOvenTemp = GetEnergnistData(energnistData, 1);
		dataT2S = GetEnergnistData(energnistData, 2);
		dataEBKTemp = GetEnergnistData(energnistData, 3);
		dataNoxCalc = GetEnergnistData(energnistData, 4);
		dataNox = GetEnergnistData(energnistData, 5);
		dataNoxSNCRFlow = GetEnergnistData(energnistData, 6);
		dataNH3 = GetEnergnistData(energnistData, 7);
		dataSmoke02 = GetEnergnistData(energnistData, 8);
		dataMainFume = GetEnergnistData(energnistData, 9);
		dataNM3Flow = GetEnergnistData(energnistData, 10);
		dataSmokeFlow = GetEnergnistData(energnistData, 11);
		dataPrimaryReg = GetEnergnistData(energnistData, 12);
		dataPrimaryAirTemp = GetEnergnistData(energnistData, 13);
		dataSecondaryOvenAir = GetEnergnistData(energnistData, 14);
		dataSecondaryFlow = GetEnergnistData(energnistData, 15);
		scalarMean = GetScalars("data/scalar_means.csv");
		scalarStd = GetScalars("data/scalar_std.csv");

		try
		{
			session = new InferenceSession("assets/DenseModelTest.onnx");
			GD.Print("ONNX model loaded successfully!");
			foreach (var input in session.InputMetadata)
			{
				GD.Print($"Input : {input.Key}, Shape: {string.Join(",", input.Value.Dimensions)}, Type: {input.Value.ElementType}");
			}
			foreach (var output in session.OutputMetadata)
			{
				GD.Print($"Ouput: {output.Key}, Shape: {string.Join(",", output.Value.Dimensions)}, Type: {output.Value.ElementType}");
			}
		}
		catch (System.Exception ex)
		{
			GD.PrintErr("Error loading ONNX model: ", ex.Message);
		}

	}
 
	public override void _Draw()
	{
		int fontSize = 14;
		Color labelColor = Colors.LightGray;
		Color axisColor = Colors.Gray;
		Color tickColor = Colors.Gray;
 
		// Axes
		DrawLine(origin, origin + new Vector2(Width, 0), axisColor, 2);
		DrawLine(origin, origin - new Vector2(0, Height), axisColor, 2);

		// Draw green filled rectangle with alpha to display miljøstyrelsen limits
		DrawRect(envelopeNOX, envelopeColor, true, -1, true);
 
		// --- X ticks: draw from persistent anchors (xLabelGlobalIdx) so labels follow points ---
		if (scaledPoints.Count > 0)
		{
			for (int a = 0; a < xLabelGlobalIdx.Count; a++)
			{
				int g = xLabelGlobalIdx[a];
				int i = IndexInVisibleByGlobal(g);
				if (i == -1) continue; // anchor not currently visible
 
				float xScreen = Margin + scaledPoints[i].X;
 
				// Tick mark
				DrawLine(new Vector2(xScreen, origin.Y - 5), new Vector2(xScreen, origin.Y + 5), tickColor);
 
				// Label (prefer formatted time, fallback to X)
				string xLabel = FormatTimeLabel(SafeGetString(visibleTimes, i));
				if (string.IsNullOrEmpty(xLabel))
					xLabel = SafeGetVector(data, i).X.ToString("0");
 
				Vector2 xSize = defaultfont.GetStringSize(text: xLabel, fontSize: fontSize);
				Vector2 xPos = new Vector2(xScreen - xSize.X / 2f, origin.Y + 20f);
				DrawString(defaultfont, xPos, xLabel, HorizontalAlignment.Left, -1, fontSize, labelColor);
			}
		}
 
		// Y ticks
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
 
		// Line series
		for (int i = 0; i < scaledPoints.Count - 1; i++)
			DrawLine(new Vector2(Margin, Margin) + scaledPoints[i], new Vector2(Margin, Margin) + scaledPoints[i + 1], Colors.Green, 2, true);
 
		// Axis labels
		Vector2 xLabelSize = defaultfont.GetStringSize("Time [HH.MM.SS]");
		Vector2 xLabelPos = new Vector2(origin.X + Width / 2 - xLabelSize.X / 2, origin.Y + Margin / 2);
		DrawString(defaultfont, xLabelPos, "Time [HH.MM.SS]", HorizontalAlignment.Left, -1, fontSize, labelColor);
 
		Vector2 yLabelSize = defaultfont.GetStringSize("nox emmision [mg/Nm3]", fontSize: fontSize);
		Vector2 yLabelPos = new Vector2(origin.X - Margin / 2, origin.Y - Height / 2 + yLabelSize.X / 2);
		DrawSetTransformMatrix(new Transform2D(Mathf.DegToRad(-90), yLabelPos));
		DrawString(defaultfont, Vector2.Zero, "nox emmision [mg/nm3]", HorizontalAlignment.Left, -1, fontSize, labelColor);
	}
 
	public float[] GetEnergnistData(string filePath, int index)
	{
		List<float> EnergnistList = new List<float>();

		using (StreamReader reader = new StreamReader(filePath))
		{
			bool firstLine = false;
			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();
				var values = line.Split(',');
				if (!firstLine) firstLine = true;
				else
				{
					EnergnistList.Add(float.Parse(values[index], CultureInfo.InvariantCulture));
				}
			}
		}
		return EnergnistList.ToArray();
	}

	public string[] GetEnergnistData(string filePath, int index, bool isDataStrings)
	{
		List<string> EnergnistList = new List<string>();

		using (StreamReader reader = new StreamReader(filePath))
		{
			bool firstLine = false;
			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();
				var values = line.Split(',');
				if (!firstLine) firstLine = true;
				else
				{
					EnergnistList.Add(values[index]);
				}
			}
		}
		return EnergnistList.ToArray();
	}

	public float[] GetScalars(String filePath)
	{
		List<float> scalarList = new List<float>();
		using (StreamReader reader = new StreamReader(filePath))
		{
			bool firstLine = false;
			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();
				var values = line.Split(',');
				if (!firstLine) firstLine = true;
				else
				{
					scalarList.Add(float.Parse(values[1], CultureInfo.InvariantCulture));
				}
			}
		}
		return scalarList.ToArray();
	}

 
	public void Plot(List<Vector2> data)
	{
		// ---- Y-axis scaling (same as before) ----
		float yDataMax = float.MinValue, yDataMin = float.MaxValue;
		foreach (var point in data)
		{
			yDataMax = Math.Max(yDataMax, point.Y);
			yDataMin = Math.Min(yDataMin, point.Y);
		}
 
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
 
		// ---- X-axis FIXED spacing (as if window were full) ----
		// There are (dataPointsMax - 1) intervals across Width for up to dataPointsMax samples
		int intervals = Math.Max(1, dataPointsMax - 1);
		float stepPx = Width / (float)intervals;
 
		// Rebuild scaled points
		scaledPoints.Clear();
		for (int i = 0; i < data.Count; i++)
		{
			float scaledX = i * stepPx;
 
			float scaledY = Height - ((data[i].Y - axisYMin) / yAxisRange) * Height;
			scaledY = Math.Max(0, Math.Min(Height, scaledY));
			if (scaledY == 0) scaledY = Height * 0.05f;
			else if (scaledY == Height) scaledY = Height * 0.95f;
 
			scaledPoints.Add(new Vector2(scaledX, scaledY));
		}

		// Scale the envelope for the NOX limits
		if (axisYMax < upperLimit)
		{
			envelopeNOX = new Rect2(new Vector2(0,0), new Vector2(0,0));
		}
		else
		{
			float envelopeY = Height - ((upperLimit - axisYMin) / yAxisRange) * Height;
			envelopeY = Math.Max(0, Math.Min(Height, envelopeY));
			envelopeNOX = new Rect2(new Vector2(Margin, Margin), new Vector2(Width, envelopeY));
		}
 
		// Update persistent x-label anchors AFTER scaling (so distances are pixel-accurate)
		UpdateXLabelsAfterScaling();
 
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
 
	private void UpdateXLabelsAfterScaling()
	{
		if (scaledPoints.Count == 0 || dataGlobalIdx.Count == 0)
		{
			xLabelGlobalIdx.Clear();
			return;
		}
 
		int firstVisibleGlobal = dataGlobalIdx[0];
		int lastVisibleGlobal  = dataGlobalIdx[dataGlobalIdx.Count - 1];
 
		// 1) Drop anchors no longer visible
		for (int a = xLabelGlobalIdx.Count - 1; a >= 0; a--)
		{
			int g = xLabelGlobalIdx[a];
			if (g < firstVisibleGlobal || g > lastVisibleGlobal)
				xLabelGlobalIdx.RemoveAt(a);
		}
 
		// 2) Ensure an anchor at the first visible point (leftmost)
		if (xLabelGlobalIdx.Count == 0)
		{
			xLabelGlobalIdx.Add(firstVisibleGlobal);
		}
		else
		{
			int iFirstAnchor = IndexInVisibleByGlobal(xLabelGlobalIdx[0]);
			if (iFirstAnchor == -1)
				xLabelGlobalIdx.Insert(0, firstVisibleGlobal);
		}
 
		// 3) Add anchor on the right when spacing is satisfied
		float spacingPx = Width / (Math.Max(2, xDesiredTicks) - 1);
 
		// Find last labeled anchor that is currently visible
		int lastLabeledVisibleGlobal = -1;
		for (int a = xLabelGlobalIdx.Count - 1; a >= 0; a--)
		{
			int g = xLabelGlobalIdx[a];
			if (IndexInVisibleByGlobal(g) != -1)
			{
				lastLabeledVisibleGlobal = g;
				break;
			}
		}
		if (lastLabeledVisibleGlobal == -1)
		{
			lastLabeledVisibleGlobal = firstVisibleGlobal;
			if (!xLabelGlobalIdx.Contains(lastLabeledVisibleGlobal))
				xLabelGlobalIdx.Add(lastLabeledVisibleGlobal);
		}
 
		int idxLastLabeled = IndexInVisibleByGlobal(lastLabeledVisibleGlobal);
		int idxNewest      = dataGlobalIdx.Count - 1;
		if (idxLastLabeled != -1 && idxNewest > idxLastLabeled)
		{
			float xLast = scaledPoints[idxLastLabeled].X;
			float xNew  = scaledPoints[idxNewest].X;
 
			// Add a bit of slack to avoid off-by-one jitter at exact spacing thresholds
			if ((xNew - xLast) + 0.001f >= spacingPx)
			{
				int gNew = dataGlobalIdx[idxNewest];
				if (xLabelGlobalIdx.Count == 0 || xLabelGlobalIdx[xLabelGlobalIdx.Count - 1] != gNew)
					xLabelGlobalIdx.Add(gNew);
			}
		}
	}
 
	private int IndexInVisibleByGlobal(int globalIdx)
	{
		for (int i = 0; i < dataGlobalIdx.Count; i++)
			if (dataGlobalIdx[i] == globalIdx) return i;
		return -1;
	}
 
	private static string FormatTimeLabel(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
		if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
			return dt.ToString("HH:mm:ss");
		return raw;
	}
 
	private static string SafeGetString(List<string> list, int idx)
	{
		if (list == null || idx < 0 || idx >= list.Count) return string.Empty;
		return list[idx];
	}
 
	private static Vector2 SafeGetVector(List<Vector2> list, int idx)
	{
		if (list == null || idx < 0 || idx >= list.Count) return Vector2.Zero;
		return list[idx];
	}
}
 
