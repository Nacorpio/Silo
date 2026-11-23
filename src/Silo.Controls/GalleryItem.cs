using Silo.Rendering;

namespace Silo;

/// <summary>What a <see cref="GalleryView"/> shows for an item when it has no <see cref="GalleryView.ItemTemplate"/>.</summary>
/// <param name="Image">The picture, or <see langword="null"/> to show a placeholder color. Its size gives the aspect ratio in the justified and masonry layouts.</param>
/// <param name="Title">The caption under the picture, or empty for none.</param>
/// <param name="Subtitle">A smaller line under the title, or <see langword="null"/>.</param>
/// <example>
/// <code language="csharp">
/// gallery.Items = photos.Select(photo => new GalleryItem(photo.Image, photo.Name, photo.Date.ToString("d"))).ToList();
/// </code>
/// </example>
public sealed record GalleryItem(IImage? Image, string Title = "", string? Subtitle = null);
