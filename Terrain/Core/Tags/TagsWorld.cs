using Friflo.Engine.ECS;


public struct WorldInitializing : ITag { }  
public struct WorldRunning : ITag { }       
public struct WorldError : ITag { }         


public struct WorldCreated : ITag { }       
public struct WorldNeedsCelestial : ITag { }
