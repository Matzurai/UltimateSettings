namespace UltimateSettings.Attributes;

/// <summary>
/// Declares that a setting cannot be edited through a settings manager.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReadonlyAttribute : Attribute
{
}