using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardSvgRenderer
{
	public static string Render(IdentityCardLayout layout, bool back, Func<IdentityCardLayoutElement, string?> value,
		Func<IdentityCardLayoutElement, bool> enabled, byte[]? logo, byte[]? photo, string qrPayload, string barcodePayload, bool strictText = true)
	{
		var builder = new StringBuilder(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{layout.Width}\" height=\"{layout.Height}\" viewBox=\"0 0 {layout.Width} {layout.Height}\"><rect width=\"{layout.Width}\" height=\"{layout.Height}\" fill=\"{IdentityCardSvgFormatting.Escape(layout.BackgroundColor)}\"/>"));
		foreach (var element in ((back ? layout.Back?.Elements : layout.Front.Elements) ?? []).Where(e => e.Visible && enabled(e)).OrderBy(e => e.Z))
		{
			var style = element.Style;
			var fill = IdentityCardSvgFormatting.Escape(style.BackgroundColor ?? layout.ThemeColor);
			var stroke = IdentityCardSvgFormatting.Escape(style.BorderColor ?? layout.ThemeColor);
			builder.Append(element.Type switch
			{
				"shape" => FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" rx=\"{style.BorderRadius ?? 0}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{style.BorderWidth ?? 0}\"/>"),
				"line" => FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" fill=\"{fill}\"/>"),
				"logo" => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, logo, layout.ThemeColor, "LOGO"),
				"photo" => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, photo, layout.ThemeColor, "PHOTO", style.PhotoShape),
				"qr" => IdentityCardMediaRenderer.RenderEmbeddedSvg(element, IdentityCardMediaRenderer.BuildQrSvg(qrPayload)),
				"barcode" => IdentityCardBarcodeRenderer.Render(element, barcodePayload),
				"text" => RenderText(element, value(element), strictText),
				_ => string.Empty
			});
		}
		return builder.Append("</svg>").ToString();
	}

	internal static string RenderText(IdentityCardLayoutElement element, string? value, bool strict)
	{
		var style = element.Style;
		value = string.IsNullOrWhiteSpace(value) ? "-" : value;
		if (style.ShowLabel == true && !string.IsNullOrWhiteSpace(element.Label)) value = $"{element.Label}{style.Separator ?? ": "}{value}";
		var size = style.FontSize ?? 9;
		var family = style.FontFamily ?? "Arial";
		var bold = style.FontWeight?.ToString() is "600" or "700" or "800" or "900" or "bold";
		using var typeface = SKTypeface.FromFamilyName(family, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
		using var font = new SKFont(typeface, size);
		var lines = new List<string>();
		foreach (var paragraph in value.Replace("\r", "").Split('\n'))
		{
			var line = "";
			foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (strict && font.MeasureText(word) > element.W + 0.5f) throw Overflow(element);
				var candidate = line.Length == 0 ? word : $"{line} {word}";
				if (line.Length > 0 && font.MeasureText(candidate) > element.W) { lines.Add(line); line = word; }
				else line = candidate;
			}
			lines.Add(line);
		}
		if (strict && lines.Count * size * 1.2f > element.H + 0.5f) throw Overflow(element);
		var anchor = style.TextAlign == "center" ? "middle" : style.TextAlign == "right" ? "end" : "start";
		var x = element.X + (anchor == "middle" ? element.W / 2 : anchor == "end" ? element.W : 0);
		var builder = new StringBuilder();
		if (style.BackgroundColor != null || (style.BorderWidth ?? 0) > 0)
			builder.Append(FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" rx=\"{style.BorderRadius ?? 0}\" fill=\"{style.BackgroundColor ?? "none"}\" stroke=\"{style.BorderColor ?? "none"}\" stroke-width=\"{style.BorderWidth ?? 0}\"/>"));
		builder.Append(FormattableString.Invariant($"<text font-family=\"{IdentityCardSvgFormatting.Escape(family)}\" font-size=\"{size}\" font-weight=\"{(bold ? 700 : 400)}\" fill=\"{style.Color ?? "#111827"}\" text-anchor=\"{anchor}\">"));
		for (var index = 0; index < lines.Count; index++)
			builder.Append(FormattableString.Invariant($"<tspan x=\"{x}\" y=\"{element.Y + size + index * size * 1.2f}\">{IdentityCardSvgFormatting.Escape(lines[index])}</tspan>"));
		return builder.Append("</text>").ToString();
	}

	private static Volo.Abp.BusinessException Overflow(IdentityCardLayoutElement element) => IdentityCardGeometry.Invalid($"Text in '{element.Label ?? element.Id}' does not fit. Enlarge the element or reduce its font size.");
}
