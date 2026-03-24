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
	private LogDispatcher _logDispatcher;
	public override void _EnterTree()
	{
		_blendFileProcessor = new BlendFileProcessor();
		AddScenePostImportPlugin(_blendFileProcessor);
		SettingRegistry.Register();
		_logDispatcher = new LogDispatcher();
		_logDispatcher.Dispatch((LogLevel.Info, "Blend processor initialized"));
	}

	public override void _ExitTree()
	{
		RemoveScenePostImportPlugin(_blendFileProcessor);
		SettingRegistry.Unregister();
		_logDispatcher.Dispatch((LogLevel.Info, "Blend processor stopped"));
	}
}
#endif
