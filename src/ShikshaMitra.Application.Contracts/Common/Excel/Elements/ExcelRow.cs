using System.Collections.Generic;

namespace ShikshaMitra.Common.Excel.Elements;

public class ExcelRow
{
	/// <summary>
	/// Gets or sets the collection of cells in the row.
	/// </summary>
	public required List<ExcelCell> Cells { get; set; } = new();

	/// <summary>
	/// Gets or sets the height of the row.
	/// </summary>
	public float? Height { get; set; }
}