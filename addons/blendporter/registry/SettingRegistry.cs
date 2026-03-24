using blendporter.definition;
using Godot;
using System.Collections.Generic;

namespace blendporter.registry;

public static class SettingRegistry
{
    public static bool Register()
    {
        if (Settings.All.Length == 0)
            return false;
        foreach (var setting in Settings.All)
            Register(setting);
        return true;
    }
    
    public static bool Register(SettingDefinition setting)
    {
        if (!Settings.NameDictionary.ContainsKey(setting.Name))
            return false;
        if (!ProjectSettings.HasSetting(setting.Name))
            ProjectSettings.SetSetting(setting.Name, setting.DefaultValue);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            {Settings.SettingNameKey, setting.Name},
            {Settings.SettingTypeKey, (int)setting.Type},
            {Settings.SettingHintKey, (int)setting.Hint},
            {Settings.SettingHintString, setting.HintString}
        });
        return true;
    }

    public static bool Register(List<SettingDefinition> settings)
    {
        if (settings.Count == 0)
            return false;
        foreach (var setting in settings)
            Register(setting);
        return true;
    }

    public static bool Unregister()
    {
        if (Settings.All.Length == 0)
            return false;
        foreach (var setting in Settings.All)
            Unregister(setting);
        return true;
    }
    
    public static bool Unregister(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name))
            return false;
        ProjectSettings.Clear(setting.Name);
        return true;
    }

    public static bool Unregister(List<SettingDefinition> settings)
    {
        if(settings.Count == 0)
            return false;
        foreach (var setting in settings)
            Unregister(setting);
        return true;
    }
    
    #nullable enable
    public static Variant? GetSetting(SettingDefinition setting)
    {
        if (!Register(Settings.NameDictionary[setting.Name]))
            return null;
        return ProjectSettings.GetSetting(setting.Name);
    }
    #nullable disable
}