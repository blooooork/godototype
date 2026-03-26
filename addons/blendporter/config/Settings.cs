using Godot;

namespace blendporter.definition;

public static class Settings
{
    public const LogLevel DefaultLogLevel = LogLevel.Warning;
    private const string LogLevelsString = "Error,Warning,Info,Debug";
    
    // Setting names
    public static readonly StringName LogLevelSetting = new("blendporter/project/log_level");
    public static readonly StringName CreateScenesSetting = new("blendporter/project/create_scenes");
    public static readonly StringName SettingNameKey = new("name");
    public static readonly StringName SettingTypeKey = new("type");
    public static readonly StringName SettingHintKey = new("hint");
    public static readonly StringName SettingHintString = new("hint_string");
    
    // Setting Definitions
    public static readonly SettingDefinition LogLevelDefinition = new(
        LogLevelSetting,
        Variant.Type.Int,
        PropertyHint.Enum,
        LogLevelsString,
        (int)DefaultLogLevel
    );

    
    public static readonly SettingDefinition CreateSceneDefinition = new(
        CreateScenesSetting,
        Variant.Type.Bool,
        PropertyHint.None,
        "",
        false
        );

    public static readonly SettingDefinition[] PersistentSettings =
    [
        LogLevelDefinition
    ];

    public static readonly SettingDefinition[] FileSettings = 
    [
        CreateSceneDefinition
    ];
}