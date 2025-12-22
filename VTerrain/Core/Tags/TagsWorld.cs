using Friflo.Engine.ECS;


public struct WorldInitializing : ITag { }  
public struct WorldRunning : ITag { }       
public struct WorldSaving : ITag { }        
public struct WorldLoading : ITag { }       
public struct WorldError : ITag { }         


public struct WorldCreated : ITag { }       
public struct WorldNeedsCelestial : ITag { }
public struct WorldNeedsSave : ITag { }     
public struct WorldNeedsLoad : ITag { }     
