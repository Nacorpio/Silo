using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Collections category.</summary>
    static IEnumerable<CatalogEntry> Collections() =>
    [
        new("GalleryView grid", "Collections", () => CreateGallery(GalleryLayout.Grid)),
        new("GalleryView justified", "Collections", () => CreateGallery(GalleryLayout.Justified, false)),
        new("GalleryView masonry", "Collections", () => CreateGallery(GalleryLayout.Masonry, false)),
        new("GalleryView strip", "Collections", () => CreateGallery(GalleryLayout.Strip)),
    ];

    /// <summary>Creates a GalleryView of colored tiles in different shapes, the third one selected.</summary>
    /// <param name="layout">The layout to show them in.</param>
    /// <param name="captions">Whether the tiles have a title and subtitle, which take room from the picture.</param>
    static GalleryView CreateGallery(GalleryLayout layout, bool captions = true)
    {
        var gallery = new GalleryView { Layout = layout, SelectionMode = GallerySelectionMode.Multiple };

        gallery.Items = Enumerable.Range(0, 14)
            .Select(i => (object)new GalleryItem(Tile(i), captions ? $"Photo {i + 1}" : string.Empty, captions && i % 2 == 0 ? "2026" : null))
            .ToList();

        gallery.SelectedIndex = 2;

        return gallery;
    }

    /// <summary>Draws a colored tile whose shape depends on its number.</summary>
    /// <param name="index">The tile's number.</param>
    static SkiaImage Tile(int index)
    {
        var (width, height) = (index % 4) switch { 0 => (160, 100), 1 => (100, 140), 2 => (120, 120), _ => (180, 100) };
        var color = SKColor.FromHsl(index * 47 % 360, 55, 55);

        return SkiaImage.Create(width, height, canvas => canvas.Clear(color));
    }
}
