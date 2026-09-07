using System.Reflection;

namespace UltimateSettings.Sources;

/// <summary>
/// A named location that can supply and optionally persist setting values, such as a JSON file or the registry.
/// </summary>
public interface ISettingsSource
{
    string Id { get; }

    bool CanRead { get; }

    bool CanWrite { get; }

    bool TryRead(string key, out object? value);

    bool TryRead(string key, Type targetType, out object? value);

    /// <summary>
    /// Persists all supplied values as one source operation.
    /// </summary>
    void WriteMany(IReadOnlyDictionary<string, object?> values);

    /// <summary>
    /// Navigates one level into a raw container value previously returned by <see cref="TryRead(string, Type, out object?)"/>
    /// (with <paramref name="propertyName"/>'s owning key read as <see cref="object"/>), extracting the value
    /// of a nested field by name. Used to resolve settings objects nested inside other settings objects,
    /// field by field, without the source's on-disk representation being read as one opaque blob.
    /// The default implementation uses reflection, which suits sources whose raw container is already a
    /// plain object graph (e.g. <see cref="InMemorySettingsSource"/>). Sources with a different raw
    /// representation (e.g. a JSON or XML document node) should override this to navigate that representation.
    /// </summary>
    bool TryNavigate(object container, string propertyName, out object? child)
    {
        var propertyInfo = container.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            child = null;
            return false;
        }

        child = propertyInfo.GetValue(container);
        return child is not null;
    }

    /// <summary>
    /// Like <see cref="TryNavigate"/>, but for the final segment of a nested field path, coercing the result
    /// to <paramref name="targetType"/>. The default implementation uses reflection; see <see cref="TryNavigate"/>
    /// for guidance on when to override it.
    /// </summary>
    bool TryNavigateLeaf(object container, string propertyName, Type targetType, out object? value)
    {
        var propertyInfo = container.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propertyInfo is null)
        {
            value = null;
            return false;
        }

        value = propertyInfo.GetValue(container);
        return true;
    }
}
