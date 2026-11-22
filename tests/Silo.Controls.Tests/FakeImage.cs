using Silo.Rendering;

/// <summary>A picture of a given size that no backend can draw, for tests that only record commands.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
sealed record FakeImage(int Width, int Height) : IImage;
