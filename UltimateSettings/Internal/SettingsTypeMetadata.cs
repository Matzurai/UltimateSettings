namespace UltimateSettings.Internal;

/// <summary>
/// Generic entry point for a root settings type's cached precedence rules. Delegates to
/// <see cref="SettingsTypeMetadataCache"/>, which also builds metadata for nested settings types
/// discovered while resolving hierarchical settings objects.
/// </summary>
internal static class SettingsTypeMetadata<TSettings>
    where TSettings : SettingsBase, new()
{
    /// <summary>
    /// The order in which properties should be resolved, respecting <see cref="Attributes.SourceOrderIfAttribute"/> dependencies.
    /// This is independent of the precedence order of the sources.
    /// </summary>
    public static IReadOnlyList<PropertyMetadata> ResolutionOrder => SettingsTypeMetadataCache.GetResolutionOrder(typeof(TSettings));

    public static IReadOnlyCollection<string> AllReferencedSourceIds => SettingsTypeMetadataCache.GetAllReferencedSourceIds(typeof(TSettings));
}
