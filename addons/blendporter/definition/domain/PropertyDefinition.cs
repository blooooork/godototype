using Godot;
using System;

namespace blendporter.definition;

public record PropertyDefinition(StringName Name, Type Type, Func<Variant, object> Convert, Action<Node, object> Apply);
