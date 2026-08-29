namespace UltimateSettings;

/// <summary>
/// Validates a newly loaded <typeparamref name="TSettings"/> candidate before it is applied to <see cref="ISettingsManager{TSettings}.Current"/>.
/// </summary>
public delegate bool SettingsValidator<TSettings>(TSettings candidate, out string? error)
    where TSettings : SettingsBase, new();
