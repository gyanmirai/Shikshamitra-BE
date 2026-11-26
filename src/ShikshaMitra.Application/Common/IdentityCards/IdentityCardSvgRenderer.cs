using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardSvgRenderer
{
	public static string Render(IdentityCardLayout layout, bool back, Func<IdentityCardLayoutElement, string?> value,
		Func<IdentityCardLayoutElement, bool> enabled, byte[]? logo, byte[]? photo, string qrPayload, string barcodePayload, bool strictText = true, byte[]? signature = null)
	{
		var builder = new StringBuilder(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{layout.Width}\" height=\"{layout.Height}\" viewBox=\"0 0 {layout.Width} {layout.Height}\"><rect width=\"{layout.Width}\" height=\"{layout.Height}\" fill=\"{IdentityCardSvgFormatting.Escape(layout.BackgroundColor)}\"/>"));
		foreach (var element in ((back ? layout.Back?.Elements : layout.Front.Elements) ?? []).Where(e => e.Visible && enabled(e)).OrderBy(e => e.Z))
		{
			var style = element.Style;
			var fill = IdentityCardSvgFormatting.Escape(style.BackgroundColor ?? layout.ThemeColor);
			var stroke = IdentityCardSvgFormatting.Escape(style.BorderColor ?? layout.ThemeColor);
			builder.Append(FormattableString.Invariant($"<g opacity=\"{element.Opacity ?? 1}\" transform=\"rotate({element.Rotation ?? 0} {element.X + element.W / 2} {element.Y + element.H / 2})\">"));
			builder.Append(element.Type switch
			{
				"shape" => RenderShape(element, fill, stroke),
				"line" when style.BorderStyle != null => RenderStyledLine(element, layout.ThemeColor),
				"line" => FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" fill=\"{fill}\"/>"),
				"logo" => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, logo, layout.ThemeColor, "LOGO"),
				"signature" when signature != null => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, signature, layout.ThemeColor, "SIGNATURE"),
				"photo" => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, photo, layout.ThemeColor, "PHOTO", style.PhotoShape),
				"image" => IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, IdentityCardRaster.Decode(element.ImageData), layout.ThemeColor, "IMAGE"),
				"qr" => IdentityCardMediaRenderer.RenderEmbeddedSvg(element, IdentityCardMediaRenderer.BuildQrSvg(qrPayload)),
				"barcode" => IdentityCardBarcodeRenderer.Render(element, barcodePayload),
				"text" => RenderText(element, value(element), strictText),
				_ => string.Empty
			});
			builder.Append("</g>");
		}
		return builder.Append("</svg>").ToString();
	}

	private static string RenderShape(IdentityCardLayoutElement element, string fill, string stroke)
	{
		var style = element.Style;
		var width = style.BorderWidth ?? 0;
		var strokePattern = StrokePattern(style.BorderStyle, width);
		return style.Shape == "ellipse"
			? FormattableString.Invariant($"<ellipse cx=\"{element.X + element.W / 2}\" cy=\"{element.Y + element.H / 2}\" rx=\"{element.W / 2}\" ry=\"{element.H / 2}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{width}\"{strokePattern}/>")
			: FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" rx=\"{style.BorderRadius ?? 0}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{width}\"{strokePattern}/>");
	}

	private static string RenderStyledLine(IdentityCardLayoutElement element, string themeColor)
	{
		var style = element.Style;
		var stroke = IdentityCardSvgFormatting.Escape(style.BorderColor ?? style.BackgroundColor ?? themeColor);
		var width = style.BorderWidth ?? element.H;
		var y = element.Y + element.H / 2;
		var strokePattern = StrokePattern(style.BorderStyle, width);
		return FormattableString.Invariant($"<line x1=\"{element.X}\" y1=\"{y}\" x2=\"{element.X + element.W}\" y2=\"{y}\" stroke=\"{stroke}\" stroke-width=\"{width}\"{strokePattern}/>");
	}

	private static string StrokePattern(string? borderStyle, float width)
	{
		if (width <= 0) return string.Empty;
		return borderStyle switch
		{
			"dashed" => FormattableString.Invariant($" stroke-dasharray=\"{4 * width} {2 * width}\" stroke-linecap=\"butt\""),
			"dotted" => FormattableString.Invariant($" stroke-dasharray=\"{0.01f * width} {2 * width}\" stroke-linecap=\"round\""),
			_ => string.Empty
		};
	}

	internal static string RenderText(IdentityCardLayoutElement element, string? value, bool strict)
	{
		var style = element.Style;
		var size = style.FontSize ?? 9;
		var bold = IsBold(style);
		var italic = style.FontStyle == "italic";
		using var typeface = GetTypeface(style);
		var family = IdentityCardPdfFonts.Register(typeface);
		using var font = new SKFont(typeface, size);
		var lines = new List<string>();
		if (!TryWrapText(element, DisplayText(element, value), font, strict, lines)) throw Overflow(element);
		var anchor = style.TextAlign == "center" ? "middle" : style.TextAlign == "right" ? "end" : "start";
		var x = element.X + (anchor == "middle" ? element.W / 2 : anchor == "end" ? element.W : 0);
		var builder = new StringBuilder();
		if (style.BackgroundColor != null || (style.BorderWidth ?? 0) > 0)
			builder.Append(FormattableString.Invariant($"<rect x=\"{element.X}\" y=\"{element.Y}\" width=\"{element.W}\" height=\"{element.H}\" rx=\"{style.BorderRadius ?? 0}\" fill=\"{style.BackgroundColor ?? "none"}\" stroke=\"{style.BorderColor ?? "none"}\" stroke-width=\"{style.BorderWidth ?? 0}\"/>"));
		builder.Append(FormattableString.Invariant($"<text font-family=\"{IdentityCardSvgFormatting.Escape(family)}\" font-size=\"{size}\" font-weight=\"{(bold ? 700 : 400)}\" font-style=\"{(italic ? "italic" : "normal")}\" fill=\"{style.Color ?? "#111827"}\" text-anchor=\"{anchor}\">"));
		for (var index = 0; index < lines.Count; index++)
			builder.Append(FormattableString.Invariant($"<tspan x=\"{x}\" y=\"{element.Y + size + index * size * 1.2f}\">{IdentityCardSvgFormatting.Escape(lines[index])}</tspan>"));
		builder.Append("</text>");
		AppendTextDecoration(builder, element, font, lines, x, anchor);
		return builder.ToString();
	}

	internal static float? FitTextFontSize(IdentityCardLayoutElement element, string? value)
	{
		using var typeface = GetTypeface(element.Style);
		using var font = new SKFont(typeface, element.Style.FontSize ?? 9);
		var text = DisplayText(element, value);
		var lines = new List<string>();
		bool Fits(float size)
		{
			font.Size = size;
			lines.Clear();
			return TryWrapText(element, text, font, strict: true, lines);
		}

		var upper = font.Size;
		if (Fits(upper)) return null;

		var lower = IdentityCardSchema.MinimumFontSize;
		if (!Fits(lower)) throw Overflow(element);
		while (upper - lower > 0.1f)
		{
			var candidate = (lower + upper) / 2;
			if (Fits(candidate)) lower = candidate;
			else upper = candidate;
		}
		return lower;
	}

	private static bool IsBold(IdentityCardElementStyle style) =>
		style.FontWeight?.ToString() is "600" or "700" or "800" or "900" or "bold";

	private static SKTypeface GetTypeface(IdentityCardElementStyle style)
	{
		var bold = IsBold(style);
		var italic = style.FontStyle == "italic";
		var fontStyle = italic
			? bold ? SKFontStyle.BoldItalic : SKFontStyle.Italic
			: bold ? SKFontStyle.Bold : SKFontStyle.Normal;
		return IdentityCardPdfFonts.Resolve(style.FontFamily ?? "Arial", fontStyle, bold);
	}

	private static string DisplayText(IdentityCardLayoutElement element, string? value)
	{
		var text = string.IsNullOrWhiteSpace(value) ? "-" : value;
		if (element.Prefix is not null && element.FieldKey is not (null or "" or "static")) return element.Prefix + text;
		return element.Style.ShowLabel == true && !string.IsNullOrWhiteSpace(element.Label)
			? $"{element.Label}{element.Style.Separator ?? ": "}{text}"
			: text;
	}

	private static bool TryWrapText(IdentityCardLayoutElement element, string text, SKFont font, bool strict, List<string> lines)
	{
		foreach (var paragraph in text.Replace("\r", "").Split('\n'))
		{
			var line = "";
			foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (strict && font.MeasureText(word) > element.W + 0.5f) return false;
				var candidate = line.Length == 0 ? word : $"{line} {word}";
				if (line.Length > 0 && font.MeasureText(candidate) > element.W) { lines.Add(line); line = word; }
				else line = candidate;
			}
			lines.Add(line);
		}
		return !strict || lines.Count * font.Size * 1.2f <= element.H + 0.5f;
	}

	private static void AppendTextDecoration(StringBuilder builder, IdentityCardLayoutElement element, SKFont font, IReadOnlyList<string> lines, float anchorX, string anchor)
	{
		var decoration = element.Style.TextDecoration;
		if (decoration is not ("underline" or "line-through")) return;
		var metrics = font.Metrics;
		var offset = decoration == "underline" ? metrics.UnderlinePosition ?? font.Size * 0.1f : metrics.StrikeoutPosition ?? -font.Size * 0.3f;
		var thickness = Math.Max(0.5f, (decoration == "underline" ? metrics.UnderlineThickness : metrics.StrikeoutThickness) ?? font.Size / 14);
		for (var index = 0; index < lines.Count; index++)
		{
			var width = font.MeasureText(lines[index]);
			if (width <= 0) continue;
			var start = anchorX - (anchor == "middle" ? width / 2 : anchor == "end" ? width : 0);
			var y = element.Y + font.Size + index * font.Size * 1.2f + offset;
			builder.Append(FormattableString.Invariant($"<line x1=\"{start}\" y1=\"{y}\" x2=\"{start + width}\" y2=\"{y}\" stroke=\"{element.Style.Color ?? "#111827"}\" stroke-width=\"{thickness}\"/>"));
		}
	}

	private static Volo.Abp.BusinessException Overflow(IdentityCardLayoutElement element) => IdentityCardGeometry.Invalid($"Text in '{element.Label ?? element.Id}' does not fit. Enlarge the element or reduce its font size.");
}
