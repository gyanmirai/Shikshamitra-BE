using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormPrintSettingsValidation
{
	private const int SignatureImageMaxBytes = 5 * 1024 * 1024;
	private const int SignatureImageMaxBase64Characters = ((SignatureImageMaxBytes + 2) / 3) * 4;
	private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

	public static void Validate(AdmissionFormPrintAdvancedSettingsDto? settings)
	{
		if (settings == null) return;
		if (settings.Paper is { } paper)
		{
			Choice(paper.Orientation, "paper orientation", "portrait", "landscape");
			if (paper.Margins is { } margins)
				foreach (var value in new[] { margins.Top, margins.Right, margins.Bottom, margins.Left }) Range(value, 5, 30, "page margins");
		}
		if (settings.Border is { } border)
		{
			Choice(border.Placement, "border placement", "first", "every");
			Choice(border.Style, "border style", "single", "double");
			Choice(border.Thickness, "border thickness", "thin", "medium", "thick");
			Range(border.Inset, 2, 25, "border inset");
			Choice(border.Corners, "border corners", "square", "rounded");
		}
		if (settings.Style is { } style)
		{
			Choice(style.Palette, "colour palette", "school-blue", "neutral", "monochrome");
			Colour(style.SectionFill); Colour(style.BorderColor); Colour(style.TextColor);
			Choice(style.FontFamily, "font family", "sans", "serif");
			Range(style.BaseSize, 8, 14, "body font size");
			Range(style.TitleSize, 12, 24, "title font size");
			Choice(style.LabelWeight, "label weight", "regular", "semibold");
		}
		if (settings.Branding is { } branding)
		{
			Asset(branding.Logo);
			Asset(branding.Banner);
			Choice(branding.BannerPlacement, "banner placement", "first", "every", "selected");
			if (branding.BannerPlacement == "selected" && branding.BannerPages is not { Count: > 0 })
				Invalid("Select at least one page for the school banner.");
			if (branding.BannerPages is { } pages &&
				(pages.Any(page => page < 1) || pages.Distinct().Count() != pages.Count))
				Invalid("Choose unique, positive page numbers for the school banner.");
		}
		if (settings.Header is { } header)
		{
			Choice(header.Continuation, "continuation header", "compact", "full", "none");
			Choice(header.Alignment, "header alignment", "left", "center", "right");
			Text(header.Title, 128, "form title"); Text(header.Subtitle, 128, "form subtitle");
		}
		if (settings.Photo is { } photo)
		{
			Choice(photo.Placement, "photo placement", "first", "last", "field", "header-top-right");
			Choice(photo.Alignment, "photo alignment", "left", "center", "right");
			Range(photo.Width, 20, 55, "photo width"); Range(photo.Height, 25, 70, "photo height");
			Text(photo.Caption, 128, "photo caption");
		}
		if (settings.Footer is { } footer)
		{
			Choice(footer.Placement, "footer placement", "first", "every", "last", "none");
			Choice(footer.Alignment, "footer alignment", "left", "center", "right");
		}
		foreach (var section in Entries(settings.Sections, 128, "section settings"))
		{
			Range(section.Columns, 1, 4, "section columns");
			Choice(section.Spacing, "section spacing", "compact", "spacious");
			Range(section.BlankRows, 1, 30, "blank table rows");
		}
		foreach (var field in Entries(settings.Fields, 512, "field settings"))
		{
			Choice(field.Width, "field width", "auto", "half", "full");
			Choice(field.LabelPosition, "label position", "above", "inline");
			Choice(field.AnswerStyle, "answer style", "auto", "line", "boxes", "checkboxes");
			Range(field.WritingLines, 1, 8, "writing lines");
		}
		if (settings.Content is { } content)
		{
			SignatureImage(content.ParentSignatureImage, "parent signature");
			SignatureImage(content.OfficeSignatureImage, "office signature");
			Choice(content.InstructionsPlacement, "instructions placement", "first", "section", "before-declaration", "separate-page");
			Text(content.InstructionSection, 128, "instructions section");
			Choice(content.ChecklistPlacement, "checklist placement", "first", "last", "separate-page");
			Choice(content.SignaturePlacement, "signature placement", "last", "separate-page");
			Choice(content.OfficePlacement, "office-use placement", "last", "separate-page");
			foreach (var translation in Entries(content.Translations, 8, "content translations"))
			{
				Text(translation.Instructions, 2000, "instructions"); Text(translation.Declaration, 2000, "declaration");
				Text(translation.ParentLabel, 128, "parent signature label"); Text(translation.OfficeLabel, 128, "office signature label");
				if (translation.Checklist?.Count > 20) Invalid("Use at most 20 document checklist items.");
				foreach (var item in translation.Checklist ?? [])
				{
					if (item == null) Invalid("Enter text for every document checklist item.");
					Text(item, 128, "document checklist item");
				}
			}
			if (content.Translations != null && content.Translations.Keys.Any(key =>
				!Regex.IsMatch(key, "^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))))
				Invalid("Use valid language codes for print content translations.");
		}
		if (settings.Copies is { } copies)
		{
			Choice(copies.Unanswered, "unanswered field display", "blank", "dash", "not-provided");
			Choice(copies.SelectedChoices, "selected choice display", "label", "mark");
		}
	}

	private static IEnumerable<T> Entries<T>(Dictionary<string, T?>? entries, int maximum, string label) where T : class
	{
		if (entries == null) return [];
		if (entries.Count > maximum || entries.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
			Invalid($"Review {label}; use at most {maximum} unique entries.");
		foreach (var (key, value) in entries)
		{
			if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || value == null) Invalid($"Review each entry in {label}.");
		}
		return entries.Values.Select(value => value!);
	}

	private static void Asset(AdmissionPrintAssetPlacementDto? asset)
	{
		if (asset == null) return;
		Range(asset.X, 0, 100, "asset horizontal position"); Range(asset.Y, 0, 100, "asset vertical position");
		Range(asset.Width, 5, 100, "asset width"); Range(asset.Height, 5, 100, "asset height");
		if (asset.X + asset.Width > 100 || asset.Y + asset.Height > 100) Invalid("Keep the school image inside the header area.");
	}
	private static void SignatureImage(string? value, string label)
	{
		if (string.IsNullOrWhiteSpace(value)) return;
		const string pngPrefix = "data:image/png;base64,";
		const string jpegPrefix = "data:image/jpeg;base64,";
		var isPng = value.StartsWith(pngPrefix, StringComparison.Ordinal);
		var isJpeg = value.StartsWith(jpegPrefix, StringComparison.Ordinal);
		if (!isPng && !isJpeg) Invalid($"Choose a PNG or JPG image for the {label}.");
		var encoded = value[(isPng ? pngPrefix : jpegPrefix).Length..];
		if (encoded.Length == 0 || encoded.Length > SignatureImageMaxBase64Characters)
			Invalid($"Keep the {label} image at or below 5 MB.");
		byte[] bytes;
		try { bytes = Convert.FromBase64String(encoded); }
		catch (FormatException) { Invalid($"Choose a valid image for the {label}."); return; }
		if (bytes.Length == 0 || bytes.Length > SignatureImageMaxBytes)
			Invalid($"Keep the {label} image at or below 5 MB.");
		var validImage = isPng
			? bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)
			: bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
		if (!validImage) Invalid($"Choose a valid image for the {label}.");
	}
	private static void Choice(string? value, string label, params string[] allowed)
	{
		if (value != null && !allowed.Contains(value, StringComparer.Ordinal)) Invalid($"Choose a supported {label}.");
	}
	private static void Range(double? value, double minimum, double maximum, string label)
	{
		if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < minimum || value.Value > maximum))
			Invalid($"Keep {label} between {minimum} and {maximum}.");
	}
	private static void Text(string? value, int maximum, string label)
	{
		if (value?.Length > maximum) Invalid($"Keep {label} within {maximum} characters.");
	}
	private static void Colour(string? value)
	{
		if (value != null && !Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
			Invalid("Choose a six-digit hexadecimal print colour.");
	}
	private static void Invalid(string message) => throw new BusinessException(AdmissionFormErrorCodes.InvalidDefinition, message);
}
