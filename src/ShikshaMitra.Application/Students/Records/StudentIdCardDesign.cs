using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Common.IdentityCards;
using Volo.Abp;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.Records;

internal static class StudentIdCardDesign
{
	private static readonly string[] LegacyStudentFields = ["StudentName", "GRNumber", "Class", "Division", "AcademicYear", "DateOfBirth"];
	private static readonly string[] StudentFields = [.. LegacyStudentFields, "RollNo"];
	private static readonly string[] Bindings = [.. StudentFields, "CardNumber", "schoolName", "schoolAddress", "headerText", "footerText", "static", "photo"];
	private static readonly JsonSerializerOptions LayoutJsonOptions = new(IdentityCardSchema.JsonOptions) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	public static bool HasValidStudentFields(IReadOnlyCollection<string>? fields, int schemaVersion)
	{
		var allowed = schemaVersion == 2 ? StudentFields : LegacyStudentFields;
		return fields is { Count: > 0 } && fields.Count <= allowed.Length &&
			fields.Distinct().Count() == fields.Count && fields.All(field => allowed.Contains(field));
	}

	public static (IdentityCardLayout Layout, IdentityCardPrintSettings Settings) Resolve(StudentIdCardTemplateDto template, bool cardOutput = false)
	{
		try
		{
			var layout = IdentityCardSchema.ParseLayout(template.LayoutJson ?? "", Bindings, template.SchemaVersion, template.BackSideEnabled, allowSignature: true);
			var settings = IdentityCardSchema.ParsePrint(template.PrintSettingsJson, schemaVersion: template.SchemaVersion);
			if (cardOutput)
			{
				settings.PageSize = "Card";
				settings.BackOffsetXmm = settings.BackOffsetYmm = 0;
			}
			_ = IdentityCardGeometry.Sheet(layout, settings);
			if (!template.BackSideEnabled && settings.SideMode == "back") throw IdentityCardGeometry.Invalid("Enable the back before selecting back-only output.");
			return (layout, settings);
		}
		catch (BusinessException ex) when (ex.Code == IdentityCardGeometry.InvalidConfiguration)
		{
			throw new BusinessException("Students:Records:InvalidIdTemplate").WithData("detail", ex.Data["detail"] ?? "Invalid identity card configuration.");
		}
	}

	public static void FitAssignmentStudentName(StudentIdCardTemplateDto snapshot, string? studentName)
	{
		if (snapshot.SchemaVersion != 2 || !snapshot.Fields.Contains("StudentName")) return;

		var (layout, _) = Resolve(snapshot);
		var elements = layout.Front.Elements.AsEnumerable();
		if (snapshot.BackSideEnabled && layout.Back != null) elements = elements.Concat(layout.Back.Elements);
		var changed = false;
		foreach (var element in elements.Where(e => e.Visible && e.Type == "text" && e.FieldKey == "StudentName"))
		{
			if (IdentityCardSvgRenderer.FitTextFontSize(element, studentName) is not { } size) continue;
			element.Style.FontSize = size;
			changed = true;
		}
		if (changed) snapshot.LayoutJson = JsonSerializer.Serialize(layout, LayoutJsonOptions);
	}

	public static IdentityCardArtwork Artwork(StudentIdCardDto card, byte[]? photo, byte[]? logo,
		byte[]? signature = null, bool forAssignmentPrint = false)
	{
		var template = card.TemplateSnapshot;
		var (layout, _) = Resolve(template);
		var hasCardNumber = !string.IsNullOrWhiteSpace(card.CardNumber) && card.CardNumber != "Preview";
		if (forAssignmentPrint && layout.PresetKey == "classic-identity")
			HideRepeatedClassicYear(layout, template);
		string? Value(IdentityCardLayoutElement element) => element.FieldKey switch
		{
			"schoolName" => template.SchoolName,
			"schoolAddress" => template.SchoolAddress,
			"headerText" => template.HeaderText,
			"footerText" => template.FooterText,
			"CardNumber" => card.CardNumber,
			"static" or null or "" => element.Value,
			_ => card.StudentSnapshot.GetValueOrDefault(element.FieldKey)
		};
		bool Enabled(IdentityCardLayoutElement element) => element.Type switch
		{
			"logo" => template.Logo.Mode != SchoolIdentityAssetMode.Hidden && (!forAssignmentPrint || logo is { Length: > 0 }),
			"signature" => signature != null && template.Signature is { Mode: not SchoolIdentityAssetMode.Hidden },
			"qr" => template.ShowQrCode,
			"barcode" => !forAssignmentPrint || hasCardNumber,
			"photo" => template.ShowPhoto && (!forAssignmentPrint || photo is { Length: > 0 }),
			"image" => !forAssignmentPrint || !string.IsNullOrWhiteSpace(element.ImageData),
			"text" when forAssignmentPrint && (string.IsNullOrWhiteSpace(Value(element)) ||
				(element.FieldKey == "CardNumber" && !hasCardNumber)) => false,
			_ => string.IsNullOrEmpty(element.FieldKey) || element.FieldKey is "schoolName" or "schoolAddress" or "headerText" or "footerText" or "static" or "CardNumber" || template.Fields.Contains(element.FieldKey)
		};
		string Render(bool back) => IdentityCardSvgRenderer.Render(layout, back, Value, Enabled, logo, photo, card.StudentId.ToString("N"), card.CardNumber, signature: signature);
		return new(Render(false), template.BackSideEnabled ? Render(true) : null);
	}

	private static void HideRepeatedClassicYear(IdentityCardLayout layout, StudentIdCardTemplateDto template)
	{
		if (!template.Fields.Contains("AcademicYear")) return;
		var elements = layout.Front.Elements;
		var year = elements.Find(e => e.Id == "additional-record" && e.Visible && e.Type == "text" && e.FieldKey == "AcademicYear");
		var repeat = elements.Find(e => e.Id == "validity" && e.Visible && e.Type == "text" && e.FieldKey == "AcademicYear");
		if (year != null && repeat != null && year.Prefix == repeat.Prefix && year.Label == repeat.Label &&
			year.Style.ShowLabel == repeat.Style.ShowLabel && year.Style.Separator == repeat.Style.Separator)
			repeat.Visible = false;
	}
}
