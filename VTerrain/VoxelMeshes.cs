using Godot;
using System;
using System.Collections.Generic;

public enum BlockShape
{
    Cube,
    Slope,
    CornerUp,
    CornerDown
}

public enum Direction
{
    North,
    East,
    South,
    West
}

public static class VoxelMeshes
{
    private static readonly Dictionary<(BlockShape, Direction), Mesh> _meshCache = new();

    public static void Initialize()
    {
        foreach (BlockShape shape in Enum.GetValues(typeof(BlockShape)))
        {
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                _meshCache[(shape, direction)] = CreateMesh(shape, direction);
            }
        }

        GD.Print($"VoxelMeshes: инициализировано {_meshCache.Count} мешей");
    }

    public static Mesh GetMesh(BlockShape shape, Direction direction = Direction.North)
    {
        return _meshCache[(shape, direction)];
    }

    private static Mesh CreateMesh(BlockShape shape, Direction direction)
    {
        return shape switch
        {
            BlockShape.Cube => CreateCubeMesh(),
            BlockShape.Slope => CreateSlopeMesh(direction),
            BlockShape.CornerUp => CreateCornerUpMesh(direction),
            BlockShape.CornerDown => CreateCornerDownMesh(direction),
            _ => CreateCubeMesh()
        };
    }

    private static Mesh CreateCubeMesh()
    {
        var st = CreateSurfaceTool();

        Vector3[] vertices = {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
            new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1)
        };

        AddVerticesWithUV(st, vertices);

        int[] indices = {
            0, 1, 2, 2, 3, 0,  
            5, 4, 7, 7, 6, 5,  
            3, 2, 6, 6, 7, 3,  
            4, 5, 1, 1, 0, 4,  
            4, 0, 3, 3, 7, 4,  
            1, 5, 6, 6, 2, 1   
        };

        AddIndices(st, indices);
        return CommitMesh(st);
    }

    private static Mesh CreateSlopeMesh(Direction direction)
    {
        var st = CreateSurfaceTool();

        Vector3[] vertices = {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), 
            new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1)
        };

        AddVerticesWithUV(st, vertices);

        int[] indices = {
            0, 1, 2, 0, 2, 3, 
            4, 5, 1, 4, 1, 0, 
            5, 4, 3, 5, 3, 2, 
            4, 0, 3,          
            1, 5, 2           
        };

        AddIndices(st, indices);
        return RotateMesh(CommitMesh(st), direction);
    }

    private static Mesh CreateCornerUpMesh(Direction direction)
    {
        var st = CreateSurfaceTool();

        Vector3[] vertices = {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), 
            new Vector3(0, 0, 1), new Vector3(1, 1, 0)
        };

        AddVerticesWithUV(st, vertices);

        int[] indices = {
            3, 1, 0, 3, 2, 1, 
            0, 1, 4,          
            4, 1, 2,          
            0, 2, 3, 0, 4, 2  
        };

        AddIndices(st, indices);
        return RotateMesh(CommitMesh(st), direction);
    }

    private static Mesh CreateCornerDownMesh(Direction direction)
    {
        var st = CreateSurfaceTool();

        Vector3[] vertices = {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), 
            new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(1, 1, 0), 
            new Vector3(1, 1, 1)
        };

        AddVerticesWithUV(st, vertices);

        int[] indices = {
            3, 1, 0, 3, 2, 1,  
            5, 0, 1, 5, 4, 0,  
            6, 1, 2, 6, 5, 1,  
            3, 6, 2,           
            4, 3, 0,           
            4, 6, 3, 4, 5, 6   
        };

        AddIndices(st, indices);
        return RotateMesh(CommitMesh(st), direction);
    }

    //----------------------------------------------------------------------
    private static SurfaceTool CreateSurfaceTool()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetSmoothGroup(UInt32.MaxValue);
        return st;
    }

    private static void AddVerticesWithUV(SurfaceTool st, Vector3[] vertices)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            st.SetUV(new Vector2(vertices[i].X, vertices[i].Z));
            st.AddVertex(vertices[i]);
        }
    }

    private static void AddIndices(SurfaceTool st, int[] indices)
    {
        foreach (int index in indices)
        {
            st.AddIndex(index);
        }
    }

    private static Mesh CommitMesh(SurfaceTool st)
    {
        st.GenerateNormals();
        st.GenerateTangents();
        return st.Commit();
    }

    private static Mesh RotateMesh(Mesh mesh, Direction direction)
    {
        if (direction == Direction.North)
            return mesh;

        var st = CreateSurfaceTool();
        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        var indices = (int[])arrays[(int)Mesh.ArrayType.Index];

        for (int i = 0; i < vertices.Length; i++)
        {
            var vertex = vertices[i];
            var rotated = direction switch
            {
                Direction.East => new Vector3(1 - vertex.Z, vertex.Y, vertex.X),
                Direction.South => new Vector3(1 - vertex.X, vertex.Y, 1 - vertex.Z),
                Direction.West => new Vector3(vertex.Z, vertex.Y, 1 - vertex.X),
                _ => vertex
            };

            st.SetUV(new Vector2(rotated.X, rotated.Z));
            st.AddVertex(rotated);
        }

        AddIndices(st, indices);
        return CommitMesh(st);
    }

    public static void Dispose()
    {
        foreach (var mesh in _meshCache.Values)
        {
            mesh?.Dispose();
        }
        _meshCache.Clear();
        GD.Print("VoxelMeshes: ресурсы освобождены");
    }
}