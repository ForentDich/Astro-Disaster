
using Godot;
using System;
using System.Runtime.CompilerServices;

public static class WallAutoMapper
{
    private static Vector3[] _wallVertices = new Vector3[4096 * 6];
    private static int _wallVertexCount;

    public static void GenerateWalls(
        SurfaceTool st, 
        TileType[,] tiles, 
        int[,] baseHeights, 
        float heightScale)
    {
        int width = tiles.GetLength(0);
        int height = tiles.GetLength(1);
        
        _wallVertexCount = 0;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var currentHeights = TileMeshes.GetHeights(tiles[x, y]);
                float currentBase = baseHeights[x, y] * heightScale;

                // Check Right Neighbor
                if (x < width - 1)
                {
                    float h1 = currentBase + currentHeights[1] * heightScale; // NE
                    float h2 = currentBase + currentHeights[2] * heightScale; // SE

                    var neighborHeights = TileMeshes.GetHeights(tiles[x + 1, y]);
                    float neighborBase = baseHeights[x + 1, y] * heightScale;

                    float h3 = neighborBase + neighborHeights[0] * heightScale; // NW
                    float h4 = neighborBase + neighborHeights[3] * heightScale; // SW
                    
                    if (Math.Abs(h1 - h3) > 0.001f || Math.Abs(h2 - h4) > 0.001f)
                    {
                        AddWallQuad(x + 1, y, h1, h2, h3, h4, true);
                    }
                }
                
                // Check Bottom Neighbor
                if (y < height - 1)
                {
                    float h1 = currentBase + currentHeights[3] * heightScale; // SW
                    float h2 = currentBase + currentHeights[2] * heightScale; // SE

                    var neighborHeights = TileMeshes.GetHeights(tiles[x, y + 1]);
                    float neighborBase = baseHeights[x, y + 1] * heightScale;

                    float h3 = neighborBase + neighborHeights[0] * heightScale; // NW
                    float h4 = neighborBase + neighborHeights[1] * heightScale; // NE
                    
                    if (Math.Abs(h1 - h3) > 0.001f || Math.Abs(h2 - h4) > 0.001f)
                    {
                        AddWallQuad(x, y + 1, h1, h2, h3, h4, false);
                    }
                }
            }
        }

        for (int i = 0; i < _wallVertexCount; i++)
        {
            st.AddVertex(_wallVertices[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddWallQuad(int x, int y, float h1, float h2, float h3, float h4, bool flip)
    {
        if (_wallVertexCount + 6 >= _wallVertices.Length)
        {
             Array.Resize(ref _wallVertices, _wallVertices.Length * 2);
        }

        float y1 = h1;
        float y2 = h2;
        float y3 = h3;
        float y4 = h4;
        
        // Coordinates depend on flip (vertical vs horizontal wall)
        // If flip (vertical wall between x and x+1):
        // Wall is along Z axis at X.
        // Vertices: (x, y1, y), (x, y2, y+1) ...
        
        // Wait, the original code used GetCornerPos.
        // GetCornerPos(x, y, 1, ...) -> NE -> (x+1, yPos, y)
        // GetCornerPos(x, y, 2, ...) -> SE -> (x+1, yPos, y+1)
        
        // So for vertical wall (flip=true):
        // p1a = (x, h1, y)
        // p2a = (x, h2, y+1)
        // p1b = (x, h3, y)
        // p2b = (x, h4, y+1)
        
        // For horizontal wall (flip=false):
        // p1a = (x, h1, y)
        // p2a = (x+1, h2, y)
        // p1b = (x, h3, y)
        // p2b = (x+1, h4, y)
        
        Vector3 v1, v2, v3, v4;
        
        if (flip) // Vertical wall at x
        {
            v1 = new Vector3(x, h1, y);
            v2 = new Vector3(x, h2, y + 1);
            v3 = new Vector3(x, h3, y);
            v4 = new Vector3(x, h4, y + 1);
        }
        else // Horizontal wall at y
        {
            v1 = new Vector3(x, h1, y);
            v2 = new Vector3(x + 1, h2, y);
            v3 = new Vector3(x, h3, y);
            v4 = new Vector3(x + 1, h4, y);
        }

        // Add triangles
        // Original HandleEdge:
        // if (flipNormal) { AddTriangle(st, p1a, p1b, p2a); AddTriangle(st, p2a, p1b, p2b); }
        // else { AddTriangle(st, p2a, p1b, p1a); AddTriangle(st, p2b, p1b, p2a); }
        
        // Here flip is passed as true for vertical wall (x < width - 1).
        // In original code:
        // HandleEdge(..., true) for Right Neighbor (vertical wall)
        // HandleEdge(..., false) for Bottom Neighbor (horizontal wall)
        
        if (flip)
        {
             // p1a=v1, p2a=v2, p1b=v3, p2b=v4
             // Tri 1: v1, v3, v2
             _wallVertices[_wallVertexCount++] = v1;
             _wallVertices[_wallVertexCount++] = v3;
             _wallVertices[_wallVertexCount++] = v2;
             
             // Tri 2: v2, v3, v4
             _wallVertices[_wallVertexCount++] = v2;
             _wallVertices[_wallVertexCount++] = v3;
             _wallVertices[_wallVertexCount++] = v4;
        }
        else
        {
             // p1a=v1, p2a=v2, p1b=v3, p2b=v4
             // Tri 1: v2, v3, v1
             _wallVertices[_wallVertexCount++] = v2;
             _wallVertices[_wallVertexCount++] = v3;
             _wallVertices[_wallVertexCount++] = v1;
             
             // Tri 2: v4, v3, v2
             _wallVertices[_wallVertexCount++] = v4;
             _wallVertices[_wallVertexCount++] = v3;
             _wallVertices[_wallVertexCount++] = v2;
        }
    }
}