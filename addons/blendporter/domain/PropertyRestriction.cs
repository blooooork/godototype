namespace blendporter.definition;

public abstract record PropertyRestriction;
public record LengthRestriction(int Length) : PropertyRestriction;
