using System.Collections.Generic;

namespace ShikshaMitra.Common.Excel.Elements;

public class ExcelSheet
{
	/// <summary>
	/// Gets or sets the name of the sheet.
	/// </summary>
	public required string SheetName { get; set; }

	/// <summary>
	/// Gets or sets the collection of rows in the sheet.
	/// </summary>
	public required List<ExcelRow> Rows { get; set; } = new();

	/// <summary>
	/// Gets or sets the default column width.
	/// </summary>
	public float? DefaultColumnWidth { get; set; }

	/// <summary>
	/// Gets or sets the default row height.
	/// </summary>
	public float? DefaultRowHeight { get; set; }
}