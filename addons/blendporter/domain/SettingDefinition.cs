using Godot;

namespace blendporter.definition;

public record SettingDefinition(StringName Name, Variant.Type Type, PropertyHint Hint, string HintString, Variant SettingValue);