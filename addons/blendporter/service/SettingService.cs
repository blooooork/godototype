using blendporter.definition;
using Godot;

namespace blendporter.registry;

public static class SettingService
{
    public static void Register()
    {
        if (Settings.All.Length == 0)
            return;
        foreach (var setting in Settings.All)
            Register(setting);
    }
    
    public static bool Register(SettingDefinition setting)
    {
        if (!Settings.NameDictionary.ContainsKey(setting.Name))
            return false;
        if (!ProjectSettings.HasSetting(setting.Name))
            ProjectSettings.SetSetting(setting.Name, setting.SettingValue);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            {Settings.SettingNameKey, setting.Name},
            {Settings.SettingTypeKey, (int)setting.Type},
            {Settings.SettingHintKey, (int)setting.Hint},
            {Settings.SettingHintString, setting.HintString}
        });
        return true;
    }

    public static void Unregister()
    {
        if (Settings.All.Length == 0)
            return ;
        foreach (var setting in Settings.All)
            Unregister(setting);
    }
    
    private static void Unregister(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name))
            return;
        ProjectSettings.Clear(setting.Name);
    }
    
    #nullable enable
    public static Variant? GetSetting(SettingDefinition setting)
    {
        if (!Register(Settings.NameDictionary[setting.Name]))
            return null;
        return ProjectSettings.GetSetting(setting.Name);
    }
    #nullable disable

    public static void SetSetting(SettingDefinition setting, Variant value)
    {
        if (!Settings.NameDictionary.ContainsKey(setting.Name))
            return;
        if (!ProjectSettings.HasSetting(setting.Name))
            Register(setting);
        ProjectSettings.SetSetting(setting.Name, value);
    }
}