namespace godototype.camera;

public record CameraClaim(IVirtualCamera Source, int Priority, float BlendIn =- 0.4f);