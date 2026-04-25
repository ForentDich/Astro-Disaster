using Godot;
using System;
using System.IO;

/// <summary>
/// Creates shader lookup textures from SurfaceRegistry and applies them to the terrain shader.
/// Supports default textures from res:// and optional resource-pack textures from user://.
/// </summary>
public static class TerrainTextureLoader
{
    private const int FALLBACK_SIZE = 32;

    public static void Apply(ShaderMaterial material)
    {
        if (material == null)
            return;

        var surfaces = SurfaceRegistry.Surfaces;
        int count = surfaces.Length;
        if (count == 0)
        {
            GD.PrintErr("[TerrainTextureLoader] SurfaceRegistry is empty.");
            return;
        }

        Image[] images = new Image[count];
        int width = 0;
        int height = 0;

        for (int i = 0; i < count; i++)
        {
            Image image = TryLoadImage(surfaces[i].TexturePath);
            if (image == null)
                continue;

            if (image.IsCompressed())
                image.Decompress();

            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);

            images[i] = image;

            if (width == 0 || height == 0)
            {
                width = image.GetWidth();
                height = image.GetHeight();
            }
        }

        if (width <= 0 || height <= 0)
        {
            width = FALLBACK_SIZE;
            height = FALLBACK_SIZE;
        }

        for (int i = 0; i < count; i++)
        {
            if (images[i] == null)
            {
                images[i] = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
                images[i].Fill(Colors.White);
            }
            else
            {
                if (images[i].GetWidth() != width || images[i].GetHeight() != height)
                    images[i].Resize(width, height, Image.Interpolation.Nearest);

                if (images[i].GetFormat() != Image.Format.Rgba8)
                    images[i].Convert(Image.Format.Rgba8);
            }

            images[i].GenerateMipmaps();
        }

        Texture2DArray textureArray = new Texture2DArray();
        var imageArray = new Godot.Collections.Array<Image>();
        for (int i = 0; i < count; i++)
            imageArray.Add(images[i]);

        Error err = textureArray.CreateFromImages(imageArray);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[TerrainTextureLoader] Failed to build texture array: {err}");
            return;
        }

        material.SetShaderParameter("terrain_textures", textureArray);
        material.SetShaderParameter("tint_lut", BuildTintLut(surfaces));
        material.SetShaderParameter("surface_count", count);

        GD.Print($"[TerrainTextureLoader] Applied {count} terrain surfaces");
    }

    private static Texture2D BuildTintLut(SurfaceRegistry.SurfaceEntry[] surfaces)
    {
        int count = surfaces.Length;
        Image lutImage = Image.CreateEmpty(count, 1, false, Image.Format.Rgba8);

        for (int i = 0; i < count; i++)
        {
            Color tint = surfaces[i].Tint;
            float noiseStrength = Mathf.Clamp(surfaces[i].NoiseStrength, 0f, 1f);
            lutImage.SetPixel(i, 0, new Color(tint.R, tint.G, tint.B, noiseStrength));
        }

        return ImageTexture.CreateFromImage(lutImage);
    }

    private static Image TryLoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (path.StartsWith("res://", StringComparison.Ordinal))
            {
                if (!ResourceLoader.Exists(path))
                    return null;

                Texture2D texture = GD.Load<Texture2D>(path);
                return texture?.GetImage();
            }

            if (path.StartsWith("user://", StringComparison.Ordinal))
            {
                string absolute = ProjectSettings.GlobalizePath(path);
                return LoadImageFromAbsolutePath(absolute);
            }

            if (Path.IsPathRooted(path))
                return LoadImageFromAbsolutePath(path);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TerrainTextureLoader] Failed to load image '{path}': {ex.Message}");
        }

        return null;
    }

    private static Image LoadImageFromAbsolutePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        return Image.LoadFromFile(absolutePath);
    }
}
