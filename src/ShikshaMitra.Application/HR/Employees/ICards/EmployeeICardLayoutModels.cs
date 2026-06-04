using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.ICards;

internal sealed class EmployeeICardLayout
{
	public string Orientation { get; set; } = "Horizontal";
	public int Width { get; set; }
	public int Height { get; set; }
	public string BackgroundColor { get; set; } = "#FFFFFF";
	public string ThemeColor { get; set; } = "#1D4ED8";
	public EmployeeICardLayoutSide Front { get; set; } = new();
	public EmployeeICardLayoutSide? Back { get; set; }
}

internal sealed class EmployeeICardLayoutSide
{
	public List<EmployeeICardLayoutElement> Elements { get; set; } = new();
}

internal sealed class EmployeeICardLayoutElement
{
	public string Id { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public string? FieldKey { get; set; }
	public string? Label { get; set; }
	public string? Value { get; set; }
	public float X { get; set; }
	public float Y { get; set; }
	public float W { get; set; }
	public float H { get; set; }
	public int Z { get; set; }
	public bool Visible { get; set; } = true;
	public bool Locked { get; set; }
	public EmployeeICardElementStyle Style { get; set; } = new();
}

internal sealed class EmployeeICardElementStyle
{
	public string? Color { get; set; }
	public string? BackgroundColor { get; set; }
	public string? BorderColor { get; set; }
	public float? BorderWidth { get; set; }
	public float? BorderRadius { get; set; }
	public float? FontSize { get; set; }
	public object? FontWeight { get; set; }
	public string? TextAlign { get; set; }
	public bool? ShowLabel { get; set; }
	public string? Separator { get; set; }
	public string? PhotoShape { get; set; }
}

internal sealed class EmployeeICardPrintSettings
{
	public string PageSize { get; set; } = "A4";
	public int CardsPerPage { get; set; } = 8;
	public int Margin { get; set; } = 18;
	public int Gap { get; set; } = 10;
	public bool ShowCutMarks { get; set; } = true;
}
