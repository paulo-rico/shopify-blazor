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
internal readonly record struct PolarisGridMemberAccess(string? MemberName, Type ValueType, Func<object?, object?> Getter);
