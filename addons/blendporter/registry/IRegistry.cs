using blendporter.definition;
using System.Collections.Generic;

namespace godototype.addons.blendporter.registry;

public interface IRegistry
{
    public bool Register();
    public bool Register(SettingDefinition setting);
    public bool Register(List<SettingDefinition> settings);
    
    public bool Unregister();
    public bool Unregister(SettingDefinition setting);
    public bool Unregister(List<SettingDefinition> settings);
}