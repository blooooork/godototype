using Godot;
using System;

namespace blendporter.definition;

public record PropertyDefinition(string Name, Type Type, Func<Variant, object> Convert, Action<Node, object> Apply);
