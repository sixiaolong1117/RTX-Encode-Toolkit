namespace RTX_Encode_Toolkit.Models;

public sealed class VideoInfo
{
    public int Width { get; init; }

    public int Height { get; init; }

    public double Rotation { get; init; }

    public double AverageFrameRate { get; init; }

    public double RealFrameRate { get; init; }

    public string FieldOrder { get; init; } = "unknown";

    public bool IsVariableFrameRate => AverageFrameRate > 0
        && RealFrameRate > 0
        && System.Math.Abs(AverageFrameRate - RealFrameRate) > 0.01;

    public int DisplayWidth => IsRotatedSideways ? Height : Width;

    public int DisplayHeight => IsRotatedSideways ? Width : Height;

    private bool IsRotatedSideways
    {
        get
        {
            var normalized = Rotation % 360;
            if (normalized < 0)
            {
                normalized += 360;
            }

            var rounded = (int)System.Math.Round(normalized);
            return rounded is 90 or 270;
        }
    }
}
