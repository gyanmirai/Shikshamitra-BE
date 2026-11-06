using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardSchema
{
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};
	private static readonly string[] ElementTypes = ["text", "photo", "logo", "qr", "barcode", "shape", "line"];
	private static readonly string[] Fonts = ["Arial", "sans-serif", "Inter", "Roboto", "Noto Sans", "Lato"];

	public static IdentityCardLayout ParseLayout(string json, IEnumerable<string> fields, int schemaVersion = 1, bool backEnabled = false)
	{
		if (schemaVersion is not (1 or 2) || string.IsNullOrWhiteSpace(json) || json.Length > 100_000) throw IdentityCardGeometry.Invalid("Unsupported layout schema or excessive JSON size.");
		try
		{
			var options = schemaVersion == 1 ? new JsonSerializerOptions(JsonOptions) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip } : JsonOptions;
			var layout = JsonSerializer.Deserialize<IdentityCardLayout>(json, options);
			if (schemaVersion == 1 && layout != null) layout.Orientation = string.Equals(layout.Orientation, "Vertical", StringComparison.OrdinalIgnoreCase) ? "Vertical" : string.Equals(layout.Orientation, "Horizontal", StringComparison.OrdinalIgnoreCase) ? "Horizontal" : layout.Orientation;
			if (layout == null || layout.Width is < 100 or > 2000 || layout.Height is < 100 or > 2000 ||
				layout.Orientation is not ("Horizontal" or "Vertical") || layout.BackgroundColor == null || layout.ThemeColor == null || layout.Front?.Elements == null ||
				layout.Front.Elements.Count is < 1 or > 100 || layout.Back is { Elements: null } || layout.Back?.Elements.Count > 100 ||
				(schemaVersion == 2 && (layout.WidthMm == null || layout.HeightMm == null)) ||
				(backEnabled && (layout.Back?.Elements.Count ?? 0) == 0)) throw IdentityCardGeometry.Invalid("The layout needs valid dimensions and configured sides.");
			ValidateColor(layout.BackgroundColor);
			ValidateColor(layout.ThemeColor);
			layout.BackgroundColor = IdentityCardSvgFormatting.NormalizeColor(layout.BackgroundColor);
			layout.ThemeColor = IdentityCardSvgFormatting.NormalizeColor(layout.ThemeColor);
			var allowed = fields.ToHashSet(schemaVersion == 1 ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
			foreach (var side in new[] { layout.Front, layout.Back }.Where(side => side != null))
			{
				var ids = new HashSet<string>(StringComparer.Ordinal);
				foreach (var element in side!.Elements)
				{
					if (schemaVersion == 1 && element != null) element.Type = element.Type?.ToLowerInvariant() ?? "";
					if (element == null || string.IsNullOrWhiteSpace(element.Id) || element.Id.Length > 100 || !ids.Add(element.Id) || !ElementTypes.Contains(element.Type) || element.Style == null ||
						!float.IsFinite(element.X) || !float.IsFinite(element.Y) || !float.IsFinite(element.W) || !float.IsFinite(element.H) ||
						element.X < 0 || element.Y < 0 || element.W <= 0 || element.H <= 0 || element.X + element.W > layout.Width + 0.01f || element.Y + element.H > layout.Height + 0.01f ||
						element.Z is < -1000 or > 1000 || element.Value?.Length > 2000 || element.Label?.Length > 150 ||
						(!string.IsNullOrEmpty(element.FieldKey) && !allowed.Contains(element.FieldKey))) throw IdentityCardGeometry.Invalid("Invalid element bounds, duplicate IDs, type or field binding.");
					var style = element.Style;
					ValidateColor(style.Color); ValidateColor(style.BackgroundColor); ValidateColor(style.BorderColor);
					if (style.Color != null) style.Color = IdentityCardSvgFormatting.NormalizeColor(style.Color);
					if (style.BackgroundColor != null) style.BackgroundColor = IdentityCardSvgFormatting.NormalizeColor(style.BackgroundColor);
					if (style.BorderColor != null) style.BorderColor = IdentityCardSvgFormatting.NormalizeColor(style.BorderColor);
					if (!ValidNumber(style.BorderWidth, 0, 20) || !ValidNumber(style.BorderRadius, 0, 1000) || !ValidNumber(style.FontSize, 4, 100) ||
						(style.FontFamily != null && !Fonts.Contains(style.FontFamily)) || (style.TextAlign != null && style.TextAlign is not ("left" or "center" or "right")) ||
						(style.PhotoShape != null && style.PhotoShape is not ("circle" or "square" or "rounded" or "rectangle")) || style.Separator?.Length > 20 ||
						(style.FontWeight != null && style.FontWeight.ToString() is not ("normal" or "bold" or "400" or "500" or "600" or "700" or "800" or "900")))
						throw IdentityCardGeometry.Invalid("Invalid font, colour or element style.");
				}
			}
			_ = IdentityCardGeometry.ValidatePhysicalSize(layout);
			return layout;
		}
		catch (JsonException) { throw IdentityCardGeometry.Invalid("Malformed layout JSON or unknown property."); }
	}

	public static IdentityCardPrintSettings ParsePrint(string? json, int? count = null, int schemaVersion = 2)
	{
		try
		{
			if (json?.Length > 10_000) throw IdentityCardGeometry.Invalid("Print JSON is too large.");
			var settings = string.IsNullOrWhiteSpace(json) ? new IdentityCardPrintSettings() : JsonSerializer.Deserialize<IdentityCardPrintSettings>(json, JsonOptions) ?? throw IdentityCardGeometry.Invalid("Print settings cannot be null.");
			if (schemaVersion == 1 && !string.IsNullOrWhiteSpace(json))
			{
				using var document = JsonDocument.Parse(json);
				if (!document.RootElement.EnumerateObject().Any(p => p.Name.Equals("units", StringComparison.OrdinalIgnoreCase)))
				{
					settings.Margin /= IdentityCardGeometry.PointsPerMm;
					settings.Gap /= IdentityCardGeometry.PointsPerMm;
				}
			}
			if (count.HasValue) settings.CardsPerPage = count.Value;
			IdentityCardGeometry.ValidateSettings(settings);
			return settings;
		}
		catch (JsonException) { throw IdentityCardGeometry.Invalid("Malformed print JSON or unknown property."); }
	}

	private static bool ValidNumber(float? value, float min, float max) => value == null || (float.IsFinite(value.Value) && value >= min && value <= max);
	private static void ValidateColor(string? color)
	{
		if (color == null) return;
		var hex = color.TrimStart('#');
		if (hex.Length != 6 || hex.Any(c => !Uri.IsHexDigit(c))) throw IdentityCardGeometry.Invalid("Colours must be six hexadecimal digits.");
	}
}
