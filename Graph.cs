using Godot;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;


// In-editor running
//[Tool]
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
	public List<Vector2> data = new List<Vector2>();
	public List<Vector2> scaledPoints = new List<Vector2>();
	public Vector2 origin = new Vector2(Margin, Height + Margin);



	
	public override void _PhysicsProcess(double delta)
	{
		time += delta*(double) 1;
		if (count < (int) time)
		{
			if (count > dataNox.Count())
			{
				GD.Print("no more data");
			}
			count++;
			if (data.Count() < dataPointsMax - 1)
			{
				data.Add(new Vector2((float)count * 5,dataNox[count]));
			}
			else
			{
				data.RemoveAt(0);
				data.Add(new Vector2((float)count * 5,dataNox[count]));
			}
			Plot(data);
		}

	}

	public override void _Ready()
	{
		GD.Print("hello");
		//data_read_timer = GetNode<Timer>("Timer");
		//data_read_timer.Timeout += () => UpdatePlot();
		ReadData();
		//data_read_timer.Connect(Timer.SignalName.Timeout, Callable.From(StreamPlot));
		
	}


	public override void _Draw()
	{

		DrawLine(origin, origin + new Vector2(Width, 0), Colors.Gray, 2);
		DrawLine(origin, origin - new Vector2(0, Height), Colors.Gray, 2);

		// TODO: Make the ticks follow the actual datapoints, so they slide
		// off the axis like a waterfall. Keep the distance, which means max
		// 4 ticks on the screen. Same goes for the yticks.
		/*for (int i = 1; i < Ticks; i++)
		{
			float xTick = i / (float)(Ticks - 1) * Width;
			float yTick = i / (float)(Ticks - 1) * Height;

			DrawLine(origin + new Vector2(xTick,-5), origin + new Vector2(xTick,5), Colors.Gray);
			DrawLine(origin + new Vector2(-5,-yTick), origin + new Vector2(+5,-yTick), Colors.Gray);
		}
		*/

		for (int i = 0; i < scaledPoints.Count()-1; i++)
		{
			if (i % Ticks == 0)
			{
				DrawLine(new Vector2(scaledPoints[i].X, -5 + Height), new Vector2(scaledPoints[i].X, 5 + Height), Colors.Gray);
				DrawLine(new Vector2(Margin, Margin) + new Vector2(-5, scaledPoints[i].Y), new Vector2(Margin,Margin) + new Vector2(5, scaledPoints[i].Y), Colors.Gray);

			}
			//GD.Print(scaledPoints[i]);
			//GD.Print(scaledPoints[i+1]);
			DrawLine(new Vector2(Margin, Margin) + scaledPoints[i],new Vector2(Margin, Margin) + scaledPoints[i+1], Colors.Green, 2, true);
		}
	}

	public void ReadData()
	{
		using(StreamReader reader = new StreamReader("data/complete_measurements_with_designations_20240520_000000_20240603_000000.txt"))
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
			if (scaledY == 0)
			{
				scaledY = Height * 0.05f;
			}
			else if (scaledY == Height)
			{
				scaledY = Height * 0.95f;
			}

			if (scaledPoints.Count() < dataPointsMax - 1)
			{
				scaledPoints.Add(new Vector2(scaledX,scaledY));
			}
			else 
			{
				scaledPoints.RemoveAt(0);
				scaledPoints.Add(new Vector2(scaledX,scaledY));
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
				tmp1 = array[i-1];
				array[i-1] = array[i];
			}
			else 
			{
				tmp2 = array[i-1];
				array[i-1] = tmp1;
				tmp1 = tmp2;
			}
		}

		array[arraySize-1]  = newValue;

		return;
	}
}
