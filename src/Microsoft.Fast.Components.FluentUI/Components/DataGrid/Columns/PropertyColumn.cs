// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Fast.Components.FluentUI.DataGrid.Infrastructure;

namespace Microsoft.Fast.Components.FluentUI;

/// <summary>
/// Represents a <see cref="FluentDataGrid{TGridItem}"/> column whose cells display a single value.
/// </summary>
/// <typeparam name="TGridItem">The type of data represented by each row in the grid.</typeparam>
/// <typeparam name="TProp">The type of the value being displayed in the column's cells.</typeparam>
public class PropertyColumn<TGridItem, TProp> : ColumnBase<TGridItem>, IBindableColumn<TGridItem, TProp>
{
	private Expression<Func<TGridItem, TProp>>? _lastAssignedProperty;
	private Func<TGridItem, string?>? _cellTextFunc;
	private GridSort<TGridItem>? _sortBuilder;


	public PropertyInfo? PropertyInfo { get; private set; }

	/// <summary>
	/// Defines the value to be displayed in this column's cells.
	/// </summary>
	[Parameter, EditorRequired] public Expression<Func<TGridItem, TProp>> Property { get; set; } = default!;

	/// <summary>
	/// Optionally specifies a format string for the value.
	///
	/// Using this requires the <typeparamref name="TProp"/> type to implement <see cref="IFormattable" />.
	/// </summary>
	[Parameter] public string? Format { get; set; }

	/// <summary>
	/// Optionally specifies how to compare values in this column when sorting.
	/// 
	/// Using this requires the <typeparamref name="TProp"/> type to implement <see cref="IComparable{T}"/>.
	/// </summary>
	[Parameter] public IComparer<TProp>? Comparer { get; set; } = null;

	public override GridSort<TGridItem>? SortBy
	{
		get => _sortBuilder;
		set => throw new NotSupportedException($"PropertyColumn generates this member internally. For custom sorting rules, see '{typeof(TemplateColumn<TGridItem>)}'.");
	}

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		// We have to do a bit of pre-processing on the lambda expression. Only do that if it's new or changed.
		if (_lastAssignedProperty != Property)
		{
			_lastAssignedProperty = Property;
			var compiledPropertyExpression = Property.Compile();

			if (!string.IsNullOrEmpty(Format))
			{
				// TODO: Consider using reflection to avoid having to box every value just to call IFormattable.ToString
				// For example, define a method "string Format<U>(Func<TGridItem, U> property) where U: IFormattable", and
				// then construct the closed type here with U=TProp when we know TProp implements IFormattable

				// If the type is nullable, we're interested in formatting the underlying type
				var nullableUnderlyingTypeOrNull = Nullable.GetUnderlyingType(typeof(TProp));
				if (!typeof(IFormattable).IsAssignableFrom(nullableUnderlyingTypeOrNull ?? typeof(TProp)))
				{
					throw new InvalidOperationException($"A '{nameof(Format)}' parameter was supplied, but the type '{typeof(TProp)}' does not implement '{typeof(IFormattable)}'.");
				}

				_cellTextFunc = item => ((IFormattable?)compiledPropertyExpression!(item))?.ToString(Format, null);
			}
			else
			{
				_cellTextFunc = item => compiledPropertyExpression!(item)?.ToString();
			}
			
			_sortBuilder = Comparer is not null ? GridSort<TGridItem>.ByAscending(Property, Comparer) : GridSort<TGridItem>.ByAscending(Property);
	    }

		if (Property.Body is MemberExpression memberExpression)
		{
			if (Title is null)
			{
				PropertyInfo = memberExpression.Member as PropertyInfo;
				var daText = memberExpression.Member.DeclaringType?.GetDisplayAttributeString(memberExpression.Member.Name);
				if (!string.IsNullOrEmpty(daText))
					Title = daText;
				else
					Title = memberExpression.Member.Name;
			}
		}
	}

	/// <inheritdoc />
	protected internal override void CellContent(RenderTreeBuilder builder, TGridItem item)
		=> builder.AddContent(0, _cellTextFunc!(item));

}
