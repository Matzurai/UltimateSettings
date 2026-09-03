namespace UltimateSettings;

/// <summary>
/// Marker base class for strongly typed settings models resolved by <see cref="ISettingsManager{TSettings}"/>.
/// </summary>
/// <remarks>
/// Declare properties with an <c>init</c> accessor (<c>public string Foo { get; init; }</c>) rather than
/// <c>set</c>. The resolver populates a new instance via reflection after construction, which works
/// regardless of accessor kind, but an <c>init</c> accessor prevents application code from writing to
/// <see cref="ISettingsManager{TSettings}.Current"/> after the fact. Such a write would only mutate the
/// in-memory snapshot and be silently discarded on the next reload, without ever persisting; using
/// <c>init</c> turns that mistake into a compile error and steers callers toward
/// <see cref="ISettingsManager{TSettings}.Save{TValue}"/>, which writes to a source and refreshes
/// <see cref="ISettingsManager{TSettings}.Current"/> for you.
/// </remarks>
public abstract class SettingsBase
{
}
