namespace UltimateSettings.Attributes;

/// <summary>
/// Silences the diagnostic warning that is otherwise raised when a property holds a collection whose element
/// type derives from <see cref="SettingsBase"/>. Such collections are never merged element-wise across
/// sources; the entire collection from the highest-precedence source that has a value is used as-is. Apply
/// this attribute to the property when that replace-wholesale behavior is intentional.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class SuppressArrayMergeWarningAttribute : Attribute
{
}
