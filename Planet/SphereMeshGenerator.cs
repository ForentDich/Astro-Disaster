using Godot;
using System;

public partial class SphereMeshGenerator
{
    private PlanetData data;
    private Vector3 faceNormal;
    private bool isOcean;
    private int resolution;
    private HeightCalculator heightCalculator;

    public SphereMeshGenerator(PlanetData data, Vector3 faceNormal, bool isOcean, int resolution = 32)
    {
        this.data = data;
        this.faceNormal = faceNormal.Normalized();
        this.isOcean = isOcean;
        this.resolution = Math.Clamp(resolution, 4, 128);
        this.heightCalculator = new HeightCalculator(data, isOcean);
    }

    public ArrayMesh GenerateMesh()
    {
        Vector3 axisA = new Vector3(faceNormal.Y, faceNormal.Z, faceNormal.X).Normalized();
        Vector3 axisB = faceNormal.Cross(axisA).Normalized();

        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 percent = new Vector2(x, y) / (float)(resolution - 1);
                Vector2 point2D = percent * 2.0f - Vector2.One;

                Vector3 pointOnCube = faceNormal + axisA * point2D.X + axisB * point2D.Y;
                Vector3 pointOnSphere = pointOnCube.Normalized();

                float height = heightCalculator.CalculateHeight(pointOnSphere);
                Vector3 vertex = pointOnSphere * height;
                Vector3 normal = pointOnSphere;

                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(percent);
                surfaceTool.AddVertex(vertex);
            }
        }

        for (int y = 0; y < resolution - 1; y++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = x + y * resolution;

                surfaceTool.AddIndex(i);
                surfaceTool.AddIndex(i + resolution);
                surfaceTool.AddIndex(i + resolution + 1);

                surfaceTool.AddIndex(i);
                surfaceTool.AddIndex(i + resolution + 1);
                surfaceTool.AddIndex(i + 1);
            }
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    public void SetResolution(int newResolution)
    {
        resolution = Math.Clamp(newResolution, 4, 128);
    }
}