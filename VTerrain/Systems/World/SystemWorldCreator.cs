
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using System.IO;

public class SystemWorldCreator : BaseSystem
{
    public string WorldName { get; set; } = "New World";
    public int WorldSeed { get; set; } = 42;
    public bool CreateOnStart { get; set; } = true;


    private EntityStore _store;
    private bool _worldCreated;

    protected override void OnAddStore(EntityStore store)
    {
        _store = store;
    }

    protected override void OnUpdateGroup()
    {
        if (!_worldCreated && CreateOnStart)
        {
            _CreateWorld();
            _worldCreated = true;
            Enabled = false;
        }
    }


    private void _CreateWorld()
    {
        GD.Print("[ WorldCreator ] >> Creating world...");

        try
        {
            string savePath = _PrepareSavePath();

            Entity world = _store.CreateEntity(new UniqueEntity("World"));
            world.AddComponent(new WorldData
            {
                WorldId = _GenerateId(),
                Name = WorldName,
                Seed = WorldSeed,
                SavePath = savePath,
                CreatedAt = _GetTimestamp(),
                Version = 1
            });

            world.AddTag<WorldInitializing>();
            world.AddTag<WorldCreated>();
            world.AddTag<WorldNeedsCelestial>();
            world.AddTag<WorldNeedsSave>();

            _CreateFolders(savePath);
            _SaveMetadata(world, savePath);
            
            SaveStoreToJson(savePath);

            world.RemoveTag<WorldInitializing>();
            world.AddTag<WorldRunning>();

            GD.Print($"[ WorldCreator ] >> World '{WorldName}' created");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[WorldCreator] Error: {ex.Message}");
            GD.PrintErr($"[WorldCreator] StackTrace: {ex.StackTrace}");
        }
    }

    private string _PrepareSavePath()
    {
        string safeName = WorldName
            .Replace(" ", "_")
            .Replace(":", "")
            .Replace("/", "");

        return $"user://worlds/{safeName}";
    }

    private int _GenerateId()
    {
        return (WorldSeed ^ (int)_GetTimestamp()) & 0x7FFFFFFF;
    }

    private ulong _GetTimestamp()
    {
        return (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void _CreateFolders(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        GD.Print($"[WorldCreator] Creating folder: {absolutePath}");
        
        if (DirAccess.MakeDirRecursiveAbsolute(absolutePath) == Error.Ok)
        {
            GD.Print($"[WorldCreator] Folder created successfully");
        }
        else
        {
            GD.PrintErr($"[WorldCreator] Failed to create folder");
        }
    }

    private void _SaveMetadata(Entity world, string savePath)
    {
        string metaPath = $"{savePath}/world_meta.json";
        var meta = new
        {
            name = WorldName,
            seed = WorldSeed,
            created = _GetTimestamp(),
            version = 1
        };

        GD.Print($"[WorldCreator] Metadata path: {metaPath}");
    }

    private void SaveStoreToJson(string savePath)
    {
        try
        {
            var serializer = new EntitySerializer();
            
            string jsonFilePath = $"{savePath}/entity-store.json";
            string absolutePath = ProjectSettings.GlobalizePath(jsonFilePath);
            
            GD.Print($"[WorldCreator] Saving store to JSON: {absolutePath}");
            
            using (var writeStream = new FileStream(absolutePath, FileMode.Create))
            {
                serializer.WriteStore(_store, writeStream);
                writeStream.Close();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WorldCreator] Error saving to JSON: {ex.Message}");
            GD.PrintErr($"[WorldCreator] StackTrace: {ex.StackTrace}");
        }
    }
}