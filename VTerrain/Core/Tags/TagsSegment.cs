using Friflo.Engine.ECS;

public struct SegmentNeedsLoad : ITag { }
public struct SegmentNeedsGenerate : ITag { }
public struct SegmentNeedsSave : ITag { }
public struct SegmentLoaded : ITag { }
public struct SegmentUnloaded : ITag { }
public struct SegmentVisible : ITag { }