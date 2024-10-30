using System.Collections.Generic;

namespace ShikshaMitra.Common.PDF.Elements;

public class RowElement : Element, IElement
{
	/// <summary>
	/// Gets or sets the collection of elements in the row.
	/// </summary>
	public required ICollection<IElement> Elements { get; set; } = [];

	/// <summary>
	/// Gets or sets the spacing between elements in the row.
	/// </summary>
	public float? Spacing { get; set; }
}