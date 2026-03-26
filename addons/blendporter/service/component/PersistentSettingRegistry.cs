using System.Linq;
using blendporter.definition;
using Godot;

namespace blendporter.dispatcher.worker;

public class PersistentSettingRegistry
{
    public static void Register(SettingDefinition setting)
    {
        if (!Settings.PersistentSettings.Contains(setting))
            return;
        if (!ProjectSettings.HasSetting(setting.Name))
            ProjectSettings.SetSetting(setting.Name, setting.SettingValue);
        PluginLogger.Log(LogLevel.Debug, $"Registered project setting \"{setting.Name}\"");
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            {Settings.SettingNameKey, setting.Name},
            {Settings.SettingTypeKey, (int)setting.PropertyType},
            {Settings.SettingHintKey, (int)setting.Hint},
            {Settings.SettingHintString, setting.HintString}
        });
    }
    
    #nullable enable
    public static void Unregister(SettingDefinition? setting = null)
    {
        if (setting != null)
        {
            if (ProjectSettings.HasSetting(setting.Name))
                ProjectSettings.Clear(setting.Name);
        }
        else
        {
            foreach (var s in Settings.PersistentSettings)
                Unregister(s);
        }
    }


    public static Variant? GetSetting(SettingDefinition setting)
    {
        if (ProjectSettings.HasSetting(setting.Name))
            return ProjectSettings.GetSetting(setting.Name);
        return null;
    }
    #nullable disable
}