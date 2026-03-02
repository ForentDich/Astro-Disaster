/// <summary>
/// Constants for the segment system.
/// A segment is a region file storing SIDE×SIDE chunks (like Minecraft .mca).
/// </summary>
public static class ConstantsSegment
{
    /// <summary>Chunks per side of a segment (16×16 = 256 chunks per segment)</summary>
    public const int SIDE = 16;

    /// <summary>Total chunk slots per segment</summary>
    public const int TOTAL_CHUNKS = SIDE * SIDE;

    /// <summary>Segment size in world units (16 × 96 = 1536)</summary>
    public const int WORLD_SIZE = SIDE * ChunkConstants.CHUNK_WORLD_SIZE;

    /// <summary>How many segments around the player to keep loaded</summary>
    public const int LOAD_RADIUS = 1;

    /// <summary>Beyond this radius segments get unloaded</summary>
    public const int UNLOAD_RADIUS = 3;

    // ── Binary file format ──

    /// <summary>File extension for segment files</summary>
    public const string FILE_EXTENSION = ".seg";

    /// <summary>Magic number "VSEG" in little-endian</summary>
    public const uint MAGIC = 0x47455356;

    /// <summary>Current binary format version</summary>
    public const ushort FORMAT_VERSION = 1;

    /// <summary>Header size in bytes: Magic(4) + Version(2) + Side(2)</summary>
    public const int HEADER_SIZE = 8;

    /// <summary>One offset-table entry: Offset(4) + Size(4)</summary>
    public const int OFFSET_ENTRY_SIZE = 8;

    /// <summary>Total offset table size in bytes</summary>
    public const int OFFSET_TABLE_SIZE = TOTAL_CHUNKS * OFFSET_ENTRY_SIZE;

    /// <summary>Byte offset where chunk data starts</summary>
    public const int DATA_OFFSET = HEADER_SIZE + OFFSET_TABLE_SIZE;
}
