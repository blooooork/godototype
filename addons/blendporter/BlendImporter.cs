#if TOOLS
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
		GD.Print("Blend processor initialized");
	}

	public override void _ExitTree()
	{
		RemoveScenePostImportPlugin(_blendFileProcessor);
		GD.Print( "Blend processor stopped");
	}
}
#endif
