using System.Linq.Expressions;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// What a column's <see cref="Expression{TDelegate}"/> says about the property behind it.
/// </summary>
/// <param name="MemberName">
/// The dotted member path, such as <c>Name</c> or <c>Metal.Name</c>, or <c>null</c> when the
/// expression is not rooted at the row — a computed value rather than a property.
/// </param>
/// <param name="ValueType">The type of the value the expression produces.</param>
/// <param name="Getter">The compiled accessor. Reflection never runs per cell.</param>
/// <param name="Setter">
/// Writes a value back onto the record, or <c>null</c> when the member cannot be written — a computed
/// expression, or a get-only property. This is the difference between a column that can be edited and
/// one that can only be shown.
/// </param>
internal readonly record struct PolarisGridMemberAccess(
    string? MemberName,
    Type ValueType,
    Func<object?, object?> Getter,
    Action<object?, object?>? Setter);
