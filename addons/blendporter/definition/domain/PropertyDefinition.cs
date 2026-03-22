using Godot;
using System;

namespace blendporter.definition;

public record PropertyDefinition(string Name, Func<Variant, object> ConvertValue, Action<Node, object> ApplyProperty);
