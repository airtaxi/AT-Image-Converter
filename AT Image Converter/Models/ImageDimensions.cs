namespace ImageConverterAT.Models;

public readonly record struct ImageDimensions(double Width, double Height)
{
    public static ImageDimensions Default { get; } = new(0, 0);
}