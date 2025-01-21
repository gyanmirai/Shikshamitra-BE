using ShikshaMitra.Common.Excel.Elements;
using System.Collections.Generic;

namespace ShikshaMitra.Common.Excel;

public class ExcelDocument
{
	/// <summary>
	/// Gets or sets the collection of sheets in the Excel document.
	/// </summary>
	public required List<ExcelSheet> Sheets { get; set; } = new();

	/// <summary>
	/// Gets or sets the name of the output file.
	/// <para />
	/// If not specified, the output file will be named "output.xlsx".
	/// </summary>
	public string? OutputFileName { get; set; }
}