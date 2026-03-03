using Friflo.Engine.ECS;

public struct ChunkPending : ITag { }
public struct ChunkNeedsLoad : ITag { }      // Try loading from .seg first
public struct ChunkDataReady : ITag { }
public struct ChunkComplete : ITag { }
public struct ChunkError : ITag { }

public struct NeedsMeshUpdate : ITag { }
public struct NeedsCollision : ITag { }
public struct NeedsSave : ITag { }            // Chunk data needs saving to .seg
public struct PendingRemoval : ITag { }
public struct ChunkVisible : ITag { }
public struct ChunkHidden : ITag { }
