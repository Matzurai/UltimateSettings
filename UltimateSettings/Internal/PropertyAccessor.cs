using System.Linq.Expressions;
using System.Reflection;

namespace UltimateSettings.Internal;

/// <summary>
/// Extracts the <see cref="PropertyInfo"/> targeted by a property-selector expression, enabling
/// type-safe targeted writes without stringly-typed keys or generated per-property methods.
/// </summary>
internal static class PropertyAccessor
{
    public static PropertyInfo GetProperty<TSettings, TValue>(Expression<Func<TSettings, TValue>> propertySelector)
    {
        var body = propertySelector.Body;
        if (body is UnaryExpression unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Member: PropertyInfo propertyInfo })
        {
            return propertyInfo;
        }

        throw new ArgumentException("Expression must be a simple property access, e.g. s => s.FontSize.", nameof(propertySelector));
    }
}
