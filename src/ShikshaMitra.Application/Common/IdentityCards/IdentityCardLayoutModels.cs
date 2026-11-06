using System.Collections.Generic;

namespace ShikshaMitra.Common.IdentityCards;

internal sealed class IdentityCardLayout
{
	public string Orientation { get; set; } = "Horizontal";
	public int Width { get; set; }
	public int Height { get; set; }
	public float? WidthMm { get; set; }
	public float? HeightMm { get; set; }
	public string? PresetKey { get; set; }
	public System.Text.Json.JsonElement? PaletteBindings { get; set; }
	public string BackgroundColor { get; set; } = "#FFFFFF";
	public string ThemeColor { get; set; } = "#1D4ED8";
	public IdentityCardLayoutSide Front { get; set; } = new();
	public IdentityCardLayoutSide? Back { get; set; }
}

internal sealed class IdentityCardLayoutSide
{
	public List<IdentityCardLayoutElement> Elements { get; set; } = new();
}

internal sealed class IdentityCardLayoutElement
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
	public System.Text.Json.JsonElement? PaletteBindings { get; set; }
	public IdentityCardElementStyle Style { get; set; } = new();
}

internal sealed class IdentityCardElementStyle
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
	public string? FontFamily { get; set; }
	public System.Text.Json.JsonElement? PaletteBindings { get; set; }
}

internal sealed class IdentityCardPrintSettings
{
	public string PageSize { get; set; } = "A4";
	public int CardsPerPage { get; set; } = 8;
	public string Units { get; set; } = "mm";
	public float Margin { get; set; } = 10;
	public float Gap { get; set; } = 3;
	public bool ShowCutMarks { get; set; }
	public float BleedMm { get; set; }
	public float SafeInsetMm { get; set; } = 3;
	public string DuplexFlip { get; set; } = "long-edge";
	public int BackRotation { get; set; }
	public float BackOffsetXmm { get; set; }
	public float BackOffsetYmm { get; set; }
	public string SideMode { get; set; } = "both";
}
