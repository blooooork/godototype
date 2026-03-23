using blendporter.definition;
using Godot;
using System.Collections.Generic;
using System.Linq;
using godototype.addons.blendporter.registry;

namespace blendporter.registry;

public class SettingRegistry : IRegistry
{
    // Setting Definitions
    private static readonly SettingDefinition LogLevelDefinition = new(
        Names.LogLevelSetting,
        Variant.Type.Int,
        PropertyHint.Enum,
        Defaults.LogLevelString,
        Defaults.LogLevel
    );

    private static readonly SettingDefinition[] SettingList =
    [
        LogLevelDefinition
    ];
    
    public static readonly Dictionary<StringName, List<SettingDefinition>> SettingDefinitions =
        SettingList.GroupBy(d => d.Name)
            .ToDictionary(g => g.Key, g => g.ToList());
    
    public bool Register()
    {
        if (SettingList.Length == 0)
            return false;
        foreach (var setting in SettingList)
            Register(setting);
        return true;
    }
    
    public bool Register(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name) || !SettingDefinitions.ContainsKey(setting.Name))
            return false;
        ProjectSettings.SetSetting(setting.Name, (int)setting.DefaultValue);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            {Names.SettingNameKey, setting.Name},
            {Names.SettingTypeKey, (int)setting.Type},
            {Names.SettingHintKey, (int)setting.Hint},
            {Names.SettingHintString, setting.HintString}
        });
        return true;
    }

    public bool Register(List<SettingDefinition> settings)
    {
        if (settings.Count == 0)
            return false;
        foreach (var setting in settings)
            Register(setting);
        return true;
    }

    public bool Unregister()
    {
        if (SettingDefinitions.Count == 0)
            return false;
        foreach (var setting in SettingDefinitions)
            Unregister(setting.Value);
        return true;
    }
    
    public bool Unregister(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name))
            return false;
        ProjectSettings.Clear(setting.Name);
        return true;
    }

    public bool Unregister(List<SettingDefinition> settings)
    {
        if(settings.Count == 0)
            return false;
        foreach (var setting in settings)
            Unregister(setting);
        return true;
    }
}