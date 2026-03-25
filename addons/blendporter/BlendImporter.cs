#if TOOLS
using blendporter.definition;
using blendporter.registry;
using blendporter.dispatcher;
using Godot;

namespace blendporter;

[Tool]
public partial class BlendImporter : EditorPlugin
{
	private BlendFileProcessor _blendFileProcessor;
	public override void _EnterTree()
	{
		_blendFileProcessor = new BlendFileProcessor();
		AddScenePostImportPlugin(_blendFileProcessor);
		SettingService.Register();
		PluginLogger.Log(LogLevel.Info, "Blend processor initialized");
	}

	public override void _ExitTree()
	{
		RemoveScenePostImportPlugin(_blendFileProcessor);
		SettingService.Unregister();
		PluginLogger.Log(LogLevel.Info, "Blend processor stopped");
	}
}
#endif
