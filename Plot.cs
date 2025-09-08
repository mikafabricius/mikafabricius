
public void Plot(List<Vector2> data)
{
    // Calculate new data min/max
    float yDataMin = float.MaxValue, yDataMax = float.MinValue;
    foreach (var point in data)
    {
        yDataMin = Math.Min(yDataMin, point.Y);
        yDataMax = Math.Max(yDataMax, point.Y);
    }

    // If first time, initialize axis range
    if (axisYMin == float.MaxValue || axisYMax == float.MinValue)
    {
        axisYMin = yDataMin;
        axisYMax = yDataMax;
    }

    // Calculate hysteresis band (e.g., 10% of current range or a fixed value)
    float range = axisYMax - axisYMin;
    float hysteresis = Math.Max(yHysteresis, range * 0.1f);

    // Expand axis if needed
    if (yDataMin < axisYMin - hysteresis)
        axisYMin = yDataMin;
    if (yDataMax > axisYMax + hysteresis)
        axisYMax = yDataMax;

    // Shrink axis if data is well within the current range + hysteresis
    if (yDataMin > axisYMin + hysteresis)
        axisYMin = yDataMin;
    if (yDataMax < axisYMax - hysteresis)
        axisYMax = yDataMax;

    // Now use axisYMin/axisYMax for scaling instead of yMin/yMax
    float yRange = axisYMax - axisYMin == 0 ? 1 : axisYMax - axisYMin;
    scaledPoints.Clear();
    foreach (var point in data)
    {
        float scaledX = ((point.X - xMin) / xRange) * Width;
        float scaledY = Height - ((point.Y - axisYMin) / yRange) * Height;
        // ... (rest of your scaling logic)
    }
    QueueRedraw();
}
