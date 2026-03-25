using System.Collections.Generic;
using Godot;
using System.Linq;

namespace blendporter.definition;

public static class Settings
{
    private const LogLevel DefaultLogLevel = LogLevel.Debug;
    private const string LogLevelsString = "Error,Warning,Info,Debug";
    
    // Setting names
    public static readonly StringName LogLevelSetting = new("blendporter/project/log_level");
    public static readonly StringName CreateScenesSetting = new("blendporter/project/create_scenes");
    public static readonly StringName SettingNameKey = new("name");
    public static readonly StringName SettingTypeKey = new("type");
    public static readonly StringName SettingHintKey = new("hint");
    public static readonly StringName SettingHintString = new("hint_string");
    
    // Setting Definitions
    private static readonly SettingDefinition LogLevelDefinition = new(
        LogLevelSetting,
        Variant.Type.Int,
        PropertyHint.Enum,
        LogLevelsString,
        (int)DefaultLogLevel
    );

    private static readonly SettingDefinition CreateSceneDefin = new(
        CreateScenesSetting,
        Variant.Type.Bool,
        PropertyHint.None,
        "",
        false
        );

    public static readonly SettingDefinition[] All =
    [
        LogLevelDefinition,
        CreateSceneDefin
    ];

    public static readonly Dictionary<StringName, SettingDefinition> NameDictionary =
        All.ToDictionary(sD => sD.Name, sD => sD);
}