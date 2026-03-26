using System.Collections.Generic;
using System.Linq;
using Godot;

namespace blendporter.dispatcher.worker;

public static class ImportFileLocator
{
    // TODO Should improve this further
    //          Will fire everytime on reimport event as well
    //              This isn't a blend file changing or anything that will result in a new .import file
    //          What we should do is take a hash of the res:// directory, excluding /addons and /blendporter
    //              Check that on every trigger as well before bothering to refresh cache
    private const string FileExtension = ".import";

    private static Dictionary<string, List<string>> _cache = new();
    private static bool _loaded;

    public static void Add(string sourcePath)
    {
        var fileName = sourcePath.GetFile();
        var key = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var importPath = sourcePath + ".import";
        if (!_cache.ContainsKey(key))
            _cache[key] = [];
        if (!_cache[key].Contains(importPath))
            _cache[key].Add(importPath);
    }

    public static List<string> Find(string nodeName)
    {
        Load();
        return _cache.TryGetValue(nodeName, out var paths) ? paths : [];
    }

    private static void Load(string rootPath = "res://")
    {
        if (_loaded)
            return;

        _cache = new Dictionary<string, List<string>>();
        ScanRecursive(rootPath);
        _loaded = true;
    }

    private static void ScanRecursive(string path)
    {
        var dir = DirAccess.Open(path);
        if (dir == null)
            return;

        dir.IncludeHidden = false;
        dir.ListDirBegin();

        var name = dir.GetNext();
        while (name != "")
        {
            var fullPath = path.EndsWith("/") ? $"{path}{name}" : $"{path}/{name}";
            if (dir.CurrentIsDir())
            {
                if (name != "." && name != "..")
                    ScanRecursive(fullPath);
            }
            else if (name.EndsWith(FileExtension))
            {
                var key = System.IO.Path.GetFileNameWithoutExtension(name.Replace(FileExtension, ""));
                if (!_cache.ContainsKey(key))
                    _cache[key] = [];
                if (!_cache[key].Contains(fullPath))
                    _cache[key].Add(fullPath);
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();
    }
    
    public static Dictionary<string, List<string>> All()
    {
        Load();
        return _cache;
    }

    public static List<string> Valid()
    {
        return All()
            .Where(kv => kv.Value.Count == 1)
            .Select(kv => kv.Value[0])
            .ToList();
    }
}