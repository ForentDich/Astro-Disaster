
using Godot;
using System;

public static class WallAutoMapper
{
    public static void GenerateWalls(SurfaceTool st, TileType[,] tiles, int[,] baseHeights, float heightScale)
    {

        int width = tiles.GetLength(0);
        int height = tiles.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Check Right Neighbor
                if (x < width - 1)
                {
                    HandleEdge(st, 
                        GetCornerPos(x, y, 1, tiles, baseHeights, heightScale), // Current NE
                        GetCornerPos(x, y, 2, tiles, baseHeights, heightScale), // Current SE
                        GetCornerPos(x + 1, y, 0, tiles, baseHeights, heightScale), // Neighbor NW
                        GetCornerPos(x + 1, y, 3, tiles, baseHeights, heightScale),  // Neighbor SW
                        true 
                    );
                }

                // Check Bottom Neighbor
                if (y < height - 1)
                {
                    HandleEdge(st,
                        GetCornerPos(x, y, 3, tiles, baseHeights, heightScale), // Current SW
                        GetCornerPos(x, y, 2, tiles, baseHeights, heightScale), // Current SE
                        GetCornerPos(x, y + 1, 0, tiles, baseHeights, heightScale), // Neighbor NW
                        GetCornerPos(x, y + 1, 1, tiles, baseHeights, heightScale),  // Neighbor NE
                        false
                    );
                }
            }
        }
    }

    private static Vector3 GetCornerPos(int x, int y, int cornerIndex, TileType[,] tiles, int[,] baseHeights, float heightScale)
    {
        var heights = TileMeshes.GetHeights(tiles[x, y]);
        float localHeight = heights[cornerIndex];
        float yPos = (baseHeights[x, y] + localHeight) * heightScale;
        

        float xOffset = (cornerIndex == 1 || cornerIndex == 2) ? 1 : 0;
        float zOffset = (cornerIndex == 2 || cornerIndex == 3) ? 1 : 0;

        return new Vector3(x + xOffset, yPos, y + zOffset);
    }

    private static void HandleEdge(SurfaceTool st, Vector3 p1a, Vector3 p2a, Vector3 p1b, Vector3 p2b, bool flipNormal)
    {
        if (flipNormal)
        {
            AddTriangle(st, p1a, p1b, p2a);
            AddTriangle(st, p2a, p1b, p2b);
        }
        else
        {
            AddTriangle(st, p2a, p1b, p1a);
            AddTriangle(st, p2b, p1b, p2a);
        }
    }

    private static void AddTriangle(SurfaceTool st, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        if (v1.IsEqualApprox(v2) || v2.IsEqualApprox(v3) || v3.IsEqualApprox(v1)) return;

        st.AddVertex(v1);
        st.AddVertex(v2);
        st.AddVertex(v3);
    }
}