using System.Linq.Expressions;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// Reads a column's declaration once, so that rendering a page of rows never reflects.
/// </summary>
/// <remarks>
/// The value type and the member name are both derived from the expression rather than asked for
/// twice: the type is what filter values are coerced against, and the name is the column's default
/// id — so a typo in a hand-written id cannot make a filter silently point at nothing.
/// </remarks>
internal static class PolarisGridExpression
{
    /// <summary>The member name, value type and compiled getter of a column expression.</summary>
    /// <typeparam name="TItem">The row type.</typeparam>
    /// <param name="field">The declared column expression.</param>
    public static PolarisGridMemberAccess Analyse<TItem>(Expression<Func<TItem, object?>> field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var body = Unwrap(field.Body);
        var getter = field.Compile();

        string? memberName = null;

        if (body is MemberExpression member && RootedAtParameter(member, field.Parameters[0]))
        {
            memberName = BuildPath(member);
        }

        return new PolarisGridMemberAccess(memberName, body.Type, item => getter((TItem)item!));
    }

    /// <summary>
    /// Strips the boxing conversion the compiler inserts for an <c>object</c>-typed lambda, so the
    /// body is the member access rather than a <c>Convert</c> over it.
    /// </summary>
    private static Expression Unwrap(Expression body)
    {
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        return body;
    }

    private static bool RootedAtParameter(MemberExpression member, ParameterExpression parameter)
    {
        Expression? current = member;

        while (current is MemberExpression step)
        {
            current = step.Expression;
        }

        return current is ParameterExpression candidate && candidate == parameter;
    }

    private static string BuildPath(MemberExpression member)
        => member.Expression is MemberExpression inner
            ? string.Concat(BuildPath(inner), ".", member.Member.Name)
            : member.Member.Name;
}
