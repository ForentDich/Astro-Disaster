using Godot;
using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Generates wall geometry to fill vertical gaps between adjacent tiles.
/// Inner walls use same-chunk data; boundary walls use neighbor chunk data.
/// </summary>
public static class WallAutoMapper
{
    private const float H = ChunkConstants.TILE_HEIGHT;
    private const int   S = ChunkConstants.TILE_SIZE;
    private const int   STRIDE = ChunkConstants.BYTES_PER_TILE;

    /// <summary>
    /// Generates walls for size×size tiles.
    /// Inner edges (x &lt; size-1, z &lt; size-1) use data from the same chunk.
    /// Right boundary (x == size-1) uses rightNeighbor data (first column).
    /// Bottom boundary (z == size-1) uses bottomNeighbor data (first row).
    /// If a neighbor span is empty, boundary walls on that edge are skipped.
    /// </summary>
    public static void GenerateWalls(
        SurfaceTool st,
        ReadOnlySpan<byte> data,
        int size,
        ReadOnlySpan<byte> rightNeighbor,
        ReadOnlySpan<byte> bottomNeighbor)
    {
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (z * size + x) * STRIDE;
                int baseH    = data[idx];
                byte surfA   = data[idx + 2]; // surfaceId of current tile
                float[] ch   = TileMeshes.GetHeights((TileType)data[idx + 1]);

                float cNW = (baseH + ch[0]) * H;
                float cNE = (baseH + ch[1]) * H;
                float cSE = (baseH + ch[2]) * H;
                float cSW = (baseH + ch[3]) * H;

                float wx = x * S;
                float wz = z * S;

                // ── Right edge ──
                if (x < size - 1)
                {
                    int ni = (z * size + x + 1) * STRIDE;
                    int nb = data[ni];
                    byte surfB = data[ni + 2];
                    float[] nh = TileMeshes.GetHeights((TileType)data[ni + 1]);

                    EmitWallEdge(st,
                        wx + S, wz, wz + S,
                        cNE, (nb + nh[0]) * H,
                        cSE, (nb + nh[3]) * H,
                        surfA, surfB);
                }
                else if (!rightNeighbor.IsEmpty)
                {
                    int ni = (z * size + 0) * STRIDE;
                    int nb = rightNeighbor[ni];
                    byte surfB = rightNeighbor[ni + 2];
                    float[] nh = TileMeshes.GetHeights((TileType)rightNeighbor[ni + 1]);

                    EmitWallEdge(st,
                        wx + S, wz, wz + S,
                        cNE, (nb + nh[0]) * H,
                        cSE, (nb + nh[3]) * H,
                        surfA, surfB);
                }

                // ── Bottom edge ──
                if (z < size - 1)
                {
                    int ni = ((z + 1) * size + x) * STRIDE;
                    int nb = data[ni];
                    byte surfB = data[ni + 2];
                    float[] nh = TileMeshes.GetHeights((TileType)data[ni + 1]);

                    EmitWallEdge(st,
                        wz + S, wx, wx + S,
                        cSW, (nb + nh[0]) * H,
                        cSE, (nb + nh[1]) * H,
                        surfA, surfB,
                        true);
                }
                else if (!bottomNeighbor.IsEmpty)
                {
                    int ni = (0 * size + x) * STRIDE;
                    int nb = bottomNeighbor[ni];
                    byte surfB = bottomNeighbor[ni + 2];
                    float[] nh = TileMeshes.GetHeights((TileType)bottomNeighbor[ni + 1]);

                    EmitWallEdge(st,
                        wz + S, wx, wx + S,
                        cSW, (nb + nh[0]) * H,
                        cSE, (nb + nh[1]) * H,
                        surfA, surfB,
                        true);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitWallEdge(
        SurfaceTool st,
        float fixedCoord, float vary0, float vary1,
        float hA0, float hB0,
        float hA1, float hB1,
        byte surfA, byte surfB,
        bool isZEdge = false)
    {
        float top0 = MathF.Max(hA0, hB0);
        float bot0 = MathF.Min(hA0, hB0);
        float top1 = MathF.Max(hA1, hB1);
        float bot1 = MathF.Min(hA1, hB1);

        if (top0 - bot0 < 0.001f && top1 - bot1 < 0.001f)
            return;

        // Wall surface = slope variant of the higher tile's surface.
        // e.g. Grass(2) → slopeVariant=1 → Dirt  (rock/earth cliff face)
        bool aHigher = (hA0 + hA1) >= (hB0 + hB1);
        byte baseSurf = aHigher ? surfA : surfB;
        byte wallSurface = GetSlopeVariant(baseSurf);

        Vector3 tl, tr, br, bl;
        if (!isZEdge)
        {
            tl = new Vector3(fixedCoord, top0, vary0);
            tr = new Vector3(fixedCoord, top1, vary1);
            br = new Vector3(fixedCoord, bot1, vary1);
            bl = new Vector3(fixedCoord, bot0, vary0);
        }
        else
        {
            tl = new Vector3(vary0, top0, fixedCoord);
            tr = new Vector3(vary1, top1, fixedCoord);
            br = new Vector3(vary1, bot1, fixedCoord);
            bl = new Vector3(vary0, bot0, fixedCoord);
        }

        if (isZEdge) aHigher = !aHigher;

        // UV: same 0..1 × 0..1 mapping as horizontal tiles (no stretching)
        Vector2 uvTL = new Vector2(0, 0);
        Vector2 uvTR = new Vector2(1, 0);
        Vector2 uvBR = new Vector2(1, 1);
        Vector2 uvBL = new Vector2(0, 1);
        Vector2 surf = new Vector2(wallSurface, 0);

        Vector3 n;
        if (aHigher)
        {
            n = ComputeNormal(tl, bl, br);
            EmitTri(st, tl, bl, br, n, uvTL, uvBL, uvBR, surf);
            EmitTri(st, tl, br, tr, n, uvTL, uvBR, uvTR, surf);
        }
        else
        {
            n = ComputeNormal(tl, tr, br);
            EmitTri(st, tl, tr, br, n, uvTL, uvTR, uvBR, surf);
            EmitTri(st, tl, br, bl, n, uvTL, uvBR, uvBL, surf);
        }
    }

    /// <summary>
    /// Returns the slope variant surface ID for a given surface.
    /// Uses SurfaceRegistry data; falls back to the surface itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetSlopeVariant(byte surfaceId)
    {
        var surfaces = SurfaceRegistry.Surfaces;
        if (surfaceId < surfaces.Length)
        {
            int sv = surfaces[surfaceId].SlopeVariant;
            if (sv >= 0 && sv < surfaces.Length)
                return (byte)sv;
        }
        return surfaceId; // -1 or out of range → use self
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ComputeNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        return (b - a).Cross(c - a).Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitTri(
        SurfaceTool st,
        Vector3 a, Vector3 b, Vector3 c, Vector3 normal,
        Vector2 uvA, Vector2 uvB, Vector2 uvC,
        Vector2 uv2)
    {
        st.SetNormal(normal);
        st.SetUV(uvA);
        st.SetUV2(uv2);
        st.AddVertex(a);

        st.SetNormal(normal);
        st.SetUV(uvB);
        st.SetUV2(uv2);
        st.AddVertex(b);

        st.SetNormal(normal);
        st.SetUV(uvC);
        st.SetUV2(uv2);
        st.AddVertex(c);
    }
}