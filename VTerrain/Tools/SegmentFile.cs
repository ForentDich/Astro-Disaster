using Godot;
using System;
using System.IO;

/// <summary>
/// Binary region-file format for segments (analogous to Minecraft .mca).
///
/// Layout:
///   Header (8 bytes)
///     [0..3]  Magic   uint32  "VSEG"
///     [4..5]  Version uint16
///     [6..7]  Side    uint16  (chunks per side)
///
///   Offset Table  (SIDE * SIDE * 8 bytes)
///     Per chunk slot:
///       [0..3]  Offset  uint32  (0 = chunk absent)
///       [4..7]  Size    uint32
///
///   Chunk Data
///     Sequential variable-length chunk blocks.
/// </summary>
public static class SegmentFile
{
	// ────────────────────── Write / Create ──────────────────────

	/// <summary>
	/// Creates an empty segment file (header + zeroed offset table, no chunk data).
	/// </summary>
	public static void CreateEmpty(string godotPath)
	{
		string abs = ProjectSettings.GlobalizePath(godotPath);
		string dir = Path.GetDirectoryName(abs);
		if (!Directory.Exists(dir))
			Directory.CreateDirectory(dir);

		using var fs = new FileStream(abs, FileMode.Create, System.IO.FileAccess.Write);
		using var w  = new BinaryWriter(fs);

		// Header
		w.Write(ConstantsSegment.MAGIC);
		w.Write(ConstantsSegment.FORMAT_VERSION);
		w.Write((ushort)ConstantsSegment.SIDE);

		// Zeroed offset table → no chunks present
		w.Write(new byte[ConstantsSegment.OFFSET_TABLE_SIZE]);
		w.Flush();
	}

	/// <summary>
	/// Writes (or overwrites) one chunk inside a segment file.
	/// Reuses the existing slot if it fits; otherwise appends to end.
	/// </summary>
	public static void WriteChunk(string godotPath, int localX, int localZ, byte[] chunkData)
	{
		string abs = ProjectSettings.GlobalizePath(godotPath);
		int idx = localZ * ConstantsSegment.SIDE + localX;

		using var fs = new FileStream(abs, FileMode.Open, System.IO.FileAccess.ReadWrite);
		using var r  = new BinaryReader(fs);
		using var w  = new BinaryWriter(fs);

		// Validate magic
		uint magic = r.ReadUInt32();
		if (magic != ConstantsSegment.MAGIC)
			throw new InvalidDataException($"Bad segment magic 0x{magic:X8}");

		// Read current offset entry
		long entryPos = ConstantsSegment.HEADER_SIZE
					  + (long)idx * ConstantsSegment.OFFSET_ENTRY_SIZE;
		fs.Seek(entryPos, SeekOrigin.Begin);

		uint existOff  = r.ReadUInt32();
		uint existSize = r.ReadUInt32();

		// Decide where to write
		uint writeOff = (existOff != 0 && existSize >= (uint)chunkData.Length)
			? existOff
			: (uint)fs.Length;          // append

		// Write chunk bytes
		fs.Seek(writeOff, SeekOrigin.Begin);
		w.Write(chunkData);

		// Update offset table
		fs.Seek(entryPos, SeekOrigin.Begin);
		w.Write(writeOff);
		w.Write((uint)chunkData.Length);

		w.Flush();
	}

	// ────────────────────── Read ──────────────────────

	/// <summary>
	/// Writes ALL chunks for a segment in a single file operation.
	/// Much faster than calling WriteChunk 256 times.
	/// chunkData[localZ * SIDE + localX] = byte[] (null entries are skipped).
	/// </summary>
	public static void WriteFull(string godotPath, byte[][] chunkData)
	{
		if (chunkData.Length != ConstantsSegment.TOTAL_CHUNKS)
			throw new ArgumentException(
				$"Expected {ConstantsSegment.TOTAL_CHUNKS} slots, got {chunkData.Length}");

		string abs = ProjectSettings.GlobalizePath(godotPath);
		string dir = Path.GetDirectoryName(abs);
		if (!Directory.Exists(dir))
			Directory.CreateDirectory(dir);

		using var fs = new FileStream(abs, FileMode.Create, System.IO.FileAccess.Write);
		using var w  = new BinaryWriter(fs);

		// Header
		w.Write(ConstantsSegment.MAGIC);
		w.Write(ConstantsSegment.FORMAT_VERSION);
		w.Write((ushort)ConstantsSegment.SIDE);

		// Pre-calculate offsets
		uint[] offsets = new uint[ConstantsSegment.TOTAL_CHUNKS];
		uint[] sizes   = new uint[ConstantsSegment.TOTAL_CHUNKS];
		uint cursor    = (uint)ConstantsSegment.DATA_OFFSET;

		for (int i = 0; i < ConstantsSegment.TOTAL_CHUNKS; i++)
		{
			if (chunkData[i] != null && chunkData[i].Length > 0)
			{
				offsets[i] = cursor;
				sizes[i]   = (uint)chunkData[i].Length;
				cursor    += sizes[i];
			}
		}

		// Write offset table
		for (int i = 0; i < ConstantsSegment.TOTAL_CHUNKS; i++)
		{
			w.Write(offsets[i]);
			w.Write(sizes[i]);
		}

		// Write chunk data sequentially
		for (int i = 0; i < ConstantsSegment.TOTAL_CHUNKS; i++)
		{
			if (chunkData[i] != null && chunkData[i].Length > 0)
				w.Write(chunkData[i]);
		}

		w.Flush();
	}

	/// <summary>
	/// Reads one chunk from a segment file. Returns null if absent or file missing.
	/// </summary>
	public static byte[] ReadChunk(string godotPath, int localX, int localZ)
	{
		string abs = ProjectSettings.GlobalizePath(godotPath);
		if (!File.Exists(abs))
			return null;

		int idx = localZ * ConstantsSegment.SIDE + localX;

		using var fs = new FileStream(abs, FileMode.Open, System.IO.FileAccess.Read);
		using var r  = new BinaryReader(fs);

		if (r.ReadUInt32() != ConstantsSegment.MAGIC)
			return null;

		fs.Seek(ConstantsSegment.HEADER_SIZE
			  + (long)idx * ConstantsSegment.OFFSET_ENTRY_SIZE,
			  SeekOrigin.Begin);

		uint off  = r.ReadUInt32();
		uint size = r.ReadUInt32();

		if (off == 0 || size == 0)
			return null;

		fs.Seek(off, SeekOrigin.Begin);
		return r.ReadBytes((int)size);
	}

	/// <summary>
	/// Returns true if the chunk slot is occupied in the segment file.
	/// </summary>
	public static bool HasChunk(string godotPath, int localX, int localZ)
	{
		string abs = ProjectSettings.GlobalizePath(godotPath);
		if (!File.Exists(abs))
			return false;

		int idx = localZ * ConstantsSegment.SIDE + localX;

		using var fs = new FileStream(abs, FileMode.Open, System.IO.FileAccess.Read);
		using var r  = new BinaryReader(fs);

		fs.Seek(ConstantsSegment.HEADER_SIZE
			  + (long)idx * ConstantsSegment.OFFSET_ENTRY_SIZE,
			  SeekOrigin.Begin);

		return r.ReadUInt32() != 0;   // offset != 0 → present
	}

	/// <summary>
	/// Returns how many chunks are actually stored in the segment file.
	/// </summary>
	public static int CountChunks(string godotPath)
	{
		string abs = ProjectSettings.GlobalizePath(godotPath);
		if (!File.Exists(abs))
			return 0;

		using var fs = new FileStream(abs, FileMode.Open, System.IO.FileAccess.Read);
		using var r  = new BinaryReader(fs);

		if (r.ReadUInt32() != ConstantsSegment.MAGIC)
			return 0;

		fs.Seek(ConstantsSegment.HEADER_SIZE, SeekOrigin.Begin);

		int count = 0;
		for (int i = 0; i < ConstantsSegment.TOTAL_CHUNKS; i++)
		{
			if (r.ReadUInt32() != 0) count++;
			r.ReadUInt32(); // skip size
		}
		return count;
	}

	// ────────────────────── Coordinate helpers ──────────────────────

	/// <summary>
	/// Global chunk coords → segment grid coords.
	///   segX = floor(chunkX / SIDE)
	/// </summary>
	public static (int segX, int segZ) ChunkToSegment(int chunkX, int chunkZ)
	{
		return (FloorDiv(chunkX, ConstantsSegment.SIDE),
				FloorDiv(chunkZ, ConstantsSegment.SIDE));
	}

	/// <summary>
	/// Global chunk coords → local coords inside segment [0..SIDE-1].
	/// </summary>
	public static (int localX, int localZ) ChunkToLocal(int chunkX, int chunkZ)
	{
		return (Mod(chunkX, ConstantsSegment.SIDE),
				Mod(chunkZ, ConstantsSegment.SIDE));
	}

	/// <summary>
	/// World position → segment grid coords.
	/// </summary>
	public static (int segX, int segZ) WorldToSegment(Vector3 worldPos)
	{
		return (Mathf.FloorToInt(worldPos.X / ConstantsSegment.WORLD_SIZE),
				Mathf.FloorToInt(worldPos.Z / ConstantsSegment.WORLD_SIZE));
	}

	/// <summary>
	/// Builds the .seg file path for a given global chunk position.
	/// </summary>
	public static string GetSegmentFilePath(string faceStoragePath, int chunkX, int chunkZ)
	{
		var (segX, segZ) = ChunkToSegment(chunkX, chunkZ);
		string fileName = $"seg_{segX}_{segZ}{ConstantsSegment.FILE_EXTENSION}";
		return Path.Combine(faceStoragePath, fileName);
	}

	// ── Math helpers (correct floor-division for negatives) ──

	private static int FloorDiv(int a, int b)
		=> a >= 0 ? a / b : (a - b + 1) / b;

	private static int Mod(int a, int b)
	{
		int r = a % b;
		return r < 0 ? r + b : r;
	}
}
