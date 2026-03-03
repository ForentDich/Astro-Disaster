using Godot;
using System;

/// <summary>
/// Builds terrain rendering data from SurfaceRegistry and applies to ShaderMaterial:
///   - Texture2DArray from surface textures (fallback = solid white)
///   - Tint LUT texture (Nx1, each pixel = surface tint color)
///   - surface_count uniform
/// </summary>
public static class TerrainTextureLoader
{
    /// <summary>
    /// Reads SurfaceRegistry (must be loaded first), builds all lookup textures,
    /// assigns to ShaderMaterial. Call once at startup from GameSession.
    /// </summary>
    public static void Apply(ShaderMaterial material)
    {
        if (material == null) return;

        var surfaces = SurfaceRegistry.Surfaces;
        int count = surfaces.Length;
        if (count == 0)
        {
            GD.PrintErr("[TerrainTextures] No surfaces in SurfaceRegistry!");
            return;
        }

        BuildTextureArray(surfaces, count, material);
        BuildTintLut(surfaces, count, material);
        material.SetShaderParameter("surface_count", count);

        GD.Print($"[TerrainTextures] Applied {count} surfaces to material");
    }

    private static void BuildTextureArray(SurfaceRegistry.SurfaceEntry[] surfaces, int count, ShaderMaterial material)
    {
        int w = 32, h = 32;
        bool sizeFound = false;
        var images = new Image[count];

        for (int i = 0; i < count; i++)
        {
            string path = surfaces[i].TexturePath;
            if (path != null && ResourceLoader.Exists(path))
            {
                var tex = GD.Load<Texture2D>(path);
                images[i] = tex.GetImage();
                images[i].Decompress();
                if (!sizeFound)
                {
                    w = images[i].GetWidth();
                    h = images[i].GetHeight();
                    sizeFound = true;
                }
                GD.Print($"[TerrainTextures] Layer {i}: {surfaces[i].Name} ({path})");
            }
            else
            {
                images[i] = null;
                GD.Print($"[TerrainTextures] Layer {i}: {surfaces[i].Name} (tint only)");
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (images[i] != null)
            {
                if (images[i].GetWidth() != w || images[i].GetHeight() != h)
                    images[i].Resize(w, h, Image.Interpolation.Nearest);
                if (images[i].GetFormat() != Image.Format.Rgba8)
                    images[i].Convert(Image.Format.Rgba8);
            }
            else
            {
                var fallback = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
                fallback.Fill(Colors.White);
                images[i] = fallback;
            }
            images[i].GenerateMipmaps();
        }

        var texArray = new Texture2DArray();
        var imgArray = new Godot.Collections.Array<Image>();
        foreach (var img in images)
            imgArray.Add(img);

        var err = texArray.CreateFromImages(imgArray);
        if (err == Error.Ok)
            material.SetShaderParameter("terrain_textures", texArray);
        else
            GD.PrintErr($"[TerrainTextures] Failed to create Texture2DArray: {err}");
    }

    private static void BuildTintLut(SurfaceRegistry.SurfaceEntry[] surfaces, int count, ShaderMaterial material)
    {
        var img = Image.CreateEmpty(count, 1, false, Image.Format.Rgba8);
        for (int i = 0; i < count; i++)
        {
            var c = surfaces[i].Tint;
            // Alpha channel = noiseStrength (0..1)
            img.SetPixel(i, 0, new Color(c.R, c.G, c.B, surfaces[i].NoiseStrength));
        }

        material.SetShaderParameter("tint_lut", ImageTexture.CreateFromImage(img));
    }

}
