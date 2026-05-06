using Godot;
using System;

/// <summary>
/// Projects a flat cube face onto a sphere using the tangent (S2) method.
///
/// Formula:
///   1. Map vertex grid index (globalX, globalY) to [-1, +1] range on the face.
///   2. Apply tan(u * PI / 4) — tangent correction for uniform spherical distribution.
///   3. Map to cube face using FaceOrientation (Normal, Up, Right).
///   4. Normalize → project onto sphere of given radius.
///
/// This ensures seamless stitching between chunks and segments because
/// the tangent function is continuous and uses global face coordinates.
/// </summary>
public static class CubeSphereProjection
{
    /// <summary>
    /// Returns the sphere point for a vertex at (globalX, globalY) on a cube face.
    /// </summary>
    /// <param name="globalX">Vertex X index across the entire face (0..resolution).</param>
    /// <param name="globalY">Vertex Y index across the entire face (0..resolution).</param>
    /// <param name="resolution">Total vertex resolution of the face (e.g. SegmentsPerSide * SIDE * CHUNK_SIZE).</param>
    /// <param name="orientation">Face orientation (Normal, Up, Right).</param>
    /// <param name="radius">Planet radius.</param>
    /// <returns>Position on the sphere surface.</returns>
    public static Vector3 GetSpherePoint(
        int globalX, int globalY,
        int resolution,
        FaceOrientation orientation,
        float radius)
    {
        // 1. Map to [-1, +1] range on the face
        float u = (float)globalX / resolution * 2f - 1f;
        float v = (float)globalY / resolution * 2f - 1f;

        // 2. Tangent correction (S2 Projection)
        float tx = Mathf.Tan(u * Mathf.Pi / 4f);
        float ty = Mathf.Tan(v * Mathf.Pi / 4f);

        // 3. Map to cube face using the face basis vectors
        //    faceCenter + tx * faceRight * radius + ty * faceUp * radius
        //    The face normal points outward from the cube center.
        Vector3 cubePoint = orientation.Normal * radius
                          + orientation.Right * tx * radius
                          + orientation.Up * ty * radius;

        // 4. Project onto sphere
        return cubePoint.Normalized() * radius;
    }

    /// <summary>
    /// Returns the sphere point with height offset (for terrain elevation).
    /// The height is added along the sphere normal direction.
    /// </summary>
    public static Vector3 GetSpherePointWithHeight(
        int globalX, int globalY,
        int resolution,
        FaceOrientation orientation,
        float radius,
        float heightOffset)
    {
        Vector3 spherePoint = GetSpherePoint(globalX, globalY, resolution, orientation, radius);
        // Height is added along the sphere normal (outward from sphere center)
        return spherePoint.Normalized() * (radius + heightOffset);
    }

    /// <summary>
    /// Computes the face resolution (number of vertices along one side of the face).
    /// </summary>
    public static int GetFaceResolution(int segmentsPerSide)
    {
        return segmentsPerSide * ConstantsSegment.SIDE * ChunkConstants.CHUNK_SIZE;
    }

    /// <summary>
    /// Returns the world-space center of a chunk on the sphere (planet-local coordinates).
    /// </summary>
    public static Vector3 GetChunkCenterOnSphere(
        int chunkX,
        int chunkZ,
        int segmentsPerSide,
        FaceOrientation orientation,
        float radius)
    {
        int localX = ChunkConstants.CHUNK_SIZE / 2;
        int localZ = ChunkConstants.CHUNK_SIZE / 2;
        int faceResolution = GetFaceResolution(segmentsPerSide);
        var (globalX, globalZ) = GetGlobalVertexCoords(chunkX, chunkZ, localX, localZ, segmentsPerSide);
        return GetSpherePoint(globalX, globalZ, faceResolution, orientation, radius);
    }

    /// <summary>
    /// Computes the global vertex index on the face from chunk and local vertex coordinates.
    /// </summary>
    public static (int globalX, int globalY) GetGlobalVertexCoords(
        int chunkX, int chunkZ,
        int localX, int localZ,
        int segmentsPerSide)
    {
        int chunksPerSide = segmentsPerSide * ConstantsSegment.SIDE;
        int halfChunks = chunksPerSide / 2;

        // Chunk coordinates are centered (e.g. -half..half-1), so we offset to 0..chunksPerSide-1
        int chunkOffsetX = chunkX + halfChunks;
        int chunkOffsetZ = chunkZ + halfChunks;

        int globalX = chunkOffsetX * ChunkConstants.CHUNK_SIZE + localX;
        int globalZ = chunkOffsetZ * ChunkConstants.CHUNK_SIZE + localZ;

        return (globalX, globalZ);
    }

    /// <summary>
    /// Converts a world position to face-local UV coordinates on a specific face.
    /// The face-local UV coordinates are in the range [-1, +1] and represent
    /// the position on the cube face before tangent correction.
    ///
    /// This is used to determine which chunk/segment the viewer is on for each face.
    /// </summary>
    /// <param name="worldPos">World position of the viewer.</param>
    /// <param name="orientation">Face orientation (Normal, Up, Right).</param>
    /// <param name="radius">Planet radius.</param>
    /// <returns>UV coordinates in [-1, +1] range on the face.</returns>
    public static Vector2 WorldToFaceUV(Vector3 worldPos, FaceOrientation orientation, float radius)
    {
        // Project world position onto the face plane by removing the normal component
        // facePoint = worldPos projected onto the face plane
        // The face plane is defined by: dot(facePoint, normal) = radius (for the center of the face)
        // We want to find the UV coordinates such that:
        //   facePoint = normal * radius + u * right * radius + v * up * radius
        // where u, v are in [-1, +1]

        // First, get the direction from planet center to world position
        Vector3 dir = worldPos.Normalized();

        // Project onto the face basis
        // The face normal points outward. The face plane is at distance 'radius' from center.
        // For a point on the sphere, we can find its UV on the face by:
        //   u = dot(dir, right) / dot(dir, normal)
        //   v = dot(dir, up) / dot(dir, normal)
        // This is the inverse of the tangent projection.

        float dotN = dir.Dot(orientation.Normal);
        if (Mathf.Abs(dotN) < 0.0001f)
            return Vector2.Zero;

        float tx = dir.Dot(orientation.Right) / dotN;
        float ty = dir.Dot(orientation.Up) / dotN;

        // Invert tangent projection: u = atan(tx) * 4 / PI
        float u = Mathf.Atan(tx) * 4f / Mathf.Pi;
        float v = Mathf.Atan(ty) * 4f / Mathf.Pi;

        // Clamp to [-1, +1] range (safety)
        u = Mathf.Clamp(u, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        return new Vector2(u, v);
    }

    /// <summary>
    /// Converts face-local UV coordinates (in [-1, +1]) to face-local grid coordinates
    /// (in chunk units, centered at 0).
    /// </summary>
    /// <param name="uv">UV coordinates in [-1, +1] range.</param>
    /// <param name="segmentsPerSide">Number of segments per side of the face.</param>
    /// <returns>Face-local grid coordinates in chunk units.</returns>
    public static (float gridX, float gridZ) UVToFaceGrid(Vector2 uv, int segmentsPerSide)
    {
        int chunksPerSide = segmentsPerSide * ConstantsSegment.SIDE;
        float halfChunks = chunksPerSide * 0.5f;

        // UV [-1, +1] → grid [-halfChunks, +halfChunks]
        float gridX = uv.X * halfChunks;
        float gridZ = uv.Y * halfChunks;

        return (gridX, gridZ);
    }
}
