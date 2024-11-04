namespace ShikshaMitra.Common.Elements;

public class ExcelCell
{
	/// <summary>
	/// Gets or sets the value of the cell.
	/// </summary>
	public required object Value { get; set; }

	/// <summary>
	/// Gets or sets the format of the cell (e.g., number, date).
	/// </summary>
	public string? Format { get; set; }

	/// <summary>
	/// Gets or sets the background color of the cell.
	/// </summary>
	public string? BackgroundColor { get; set; }
}