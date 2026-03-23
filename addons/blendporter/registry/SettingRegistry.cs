using blendporter.definition;
using Godot;
using System.Collections.Generic;
using godototype.addons.blendporter.registry;

namespace blendporter.registry;

public class SettingRegistry : IRegistry
{
    public bool Register()
    {
        if (Settings.All.Length == 0)
            return false;
        foreach (var setting in Settings.All)
            Register(setting);
        return true;
    }
    
    public bool Register(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name) || !Settings.NameDictionary.ContainsKey(setting.Name))
            return false;
        ProjectSettings.SetSetting(setting.Name, (int)setting.DefaultValue);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            {Settings.SettingNameKey, setting.Name},
            {Settings.SettingTypeKey, (int)setting.Type},
            {Settings.SettingHintKey, (int)setting.Hint},
            {Settings.SettingHintString, setting.HintString}
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
        if (Settings.All.Length == 0)
            return false;
        foreach (var setting in Settings.All)
            Unregister(setting);
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