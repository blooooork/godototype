using Godot;
using System.Collections.Generic;

namespace blendporter.definition;

public record MatchedProperty(KeyValuePair<Variant, Variant> Entry, PropertyDefinition Definition);
