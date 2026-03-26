using Godot;

namespace blendporter.definition;

public record SettingDefinition(StringName Name, Variant.Type PropertyType, PropertyHint Hint, string HintString, Variant SettingValue);