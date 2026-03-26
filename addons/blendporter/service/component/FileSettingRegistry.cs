using blendporter.definition;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace blendporter.dispatcher.worker;

// TODO Need to make sure if a new file is dropped in that we are adding the properties to .import
// TODO Also need to figure out why metaproperty dictionaries are borked on clone to new scenes

public static class FileSettingRegistry
{
    private const string CustomSection = "params";

    private const string BlendImportExtension = ".blend.import";

    public static void Register()
    {
        ImportFileLocator
            .Valid()
            .Where(p => p.EndsWith(BlendImportExtension))
            .ToList()
            .ForEach(RegisterAllForFile);
    }

    public static void Unregister()
    {
        ImportFileLocator
            .Valid()
            .Where(p => p.EndsWith(BlendImportExtension))
            .ToList()
            .ForEach(UnregisterAllForFile);
    }

    private static void UnregisterAllForFile(string filePath)
    {
        foreach (var settingDefinition in Settings.FileSettings)
            UnregisterSettingForFile(filePath, settingDefinition);
    }

    private static void UnregisterSettingForFile(string filePath, SettingDefinition setting)
    {
        var config = new ConfigFile();
        if (config.Load(filePath) != Error.Ok)
            return;
        if (!config.HasSectionKey(CustomSection, setting.Name.ToString()))
            return;
        config.EraseSectionKey(CustomSection, setting.Name.ToString());
        config.Save(filePath);
    }

    #nullable enable
    public static Variant? GetSettingFromFile(string filePath, SettingDefinition setting)
    {
        var config = new ConfigFile();
        if (config.Load(filePath) != Error.Ok)
            return null;
        if (!config.HasSectionKey(CustomSection, setting.Name.ToString()))
            return null;
        return config.GetValue(CustomSection, setting.Name.ToString());
    }
    #nullable disable
    
    private static void RegisterAllForFile(string filePath)
    {
        if (!FileAccess.FileExists(filePath))
            return;
        foreach (var settingDefinition in Settings.FileSettings)
        {
            if (!FileHasSetting(filePath, settingDefinition))
                RegisterSettingForFile(filePath, settingDefinition);
            else
                PluginLogger.Log(LogLevel.Debug, $"File setting \"{settingDefinition.Name}\" already registered for \"{filePath}\"");
        }
    }

    private static void RegisterSettingForFile(string filePath, SettingDefinition setting)
    {
        var config = new ConfigFile();
        if (config.Load(filePath) != Error.Ok)
            return;
        config.SetValue(CustomSection, setting.Name, setting.SettingValue);
        config.Save(filePath);
        PluginLogger.Log(LogLevel.Debug, $"Registered file setting \"{setting.Name}\" for \"{filePath}\"");
    }

    private static bool FileHasSetting(string filePath, SettingDefinition setting)
    {
        var config = new ConfigFile();
        return config.Load(filePath) == Error.Ok 
               && config.HasSectionKey(CustomSection, setting.Name.ToString());
    }
}