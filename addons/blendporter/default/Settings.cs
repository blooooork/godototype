using System.Collections.Generic;
using Godot;
using System.Linq;

namespace blendporter.definition;

public class Settings
{
    public const LogLevel LogLevel = definition.LogLevel.Warning;
    private const string LogLevelString = "Error,Warning,Info,Debug";
    
    // Setting names
    public static readonly StringName LogLevelSetting = new("blendporter/debug/log_level");
    public static readonly StringName SettingNameKey = new("name");
    public static readonly StringName SettingTypeKey = new("type");
    public static readonly StringName SettingHintKey = new("hint");
    public static readonly StringName SettingHintString = new("hint_string");
    
    // Setting Definitions
    private static readonly SettingDefinition LogLevelDefinition = new(
        LogLevelSetting,
        Variant.Type.Int,
        PropertyHint.Enum,
        LogLevelString,
        LogLevel
    );

    public static readonly SettingDefinition[] All =
    [
        LogLevelDefinition
    ];

    public static readonly Dictionary<StringName, SettingDefinition> NameDictionary =
        All.ToDictionary(sD => sD.Name, sD => sD);
}