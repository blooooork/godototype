using blendporter.definition;
using blendporter.service.component;

namespace blendporter.registry;

public static class SettingService
{
    public static void Register()
    {
        // Create persistent settings
        foreach (var setting in Settings.PersistentSettings)
            PersistentSettingRegistry.Register(setting);
        // Create file based settings
        FileSettingRegistry.Register();
    }

    public static void Unregister()
    {
        PersistentSettingRegistry.Unregister();
        FileSettingRegistry.Unregister();
    }
}