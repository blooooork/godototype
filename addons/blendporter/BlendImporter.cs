#if TOOLS
using blendporter.registry;
using Godot;

namespace blendporter;

[Tool]
public partial class BlendImporter : EditorPlugin
{
	private BlendFileProcessor _blendFileProcessor;
	private SettingRegistry _settingRegistry;
	public override void _EnterTree()
	{
		_blendFileProcessor = new BlendFileProcessor();
		_settingRegistry = new SettingRegistry();
		AddScenePostImportPlugin(_blendFileProcessor);
		_settingRegistry.Register();
		GD.Print("Blend processor initialized");
	}

	public override void _ExitTree()
	{
		RemoveScenePostImportPlugin(_blendFileProcessor);
		_settingRegistry.Unregister();
		GD.Print( "Blend processor stopped");
	}
}
#endif
