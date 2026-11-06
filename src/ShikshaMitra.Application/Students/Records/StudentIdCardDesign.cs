using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common.IdentityCards;
using Volo.Abp;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.Records;

internal static class StudentIdCardDesign
{
	private static readonly string[] Bindings = ["StudentName", "GRNumber", "Class", "Division", "AcademicYear", "DateOfBirth", "CardNumber", "schoolName", "schoolAddress", "headerText", "footerText", "static", "photo"];

	public static (IdentityCardLayout Layout, IdentityCardPrintSettings Settings) Resolve(StudentIdCardTemplateDto template, bool cardOutput = false)
	{
		try
		{
			var layout = IdentityCardSchema.ParseLayout(template.LayoutJson ?? "", Bindings, template.SchemaVersion, template.BackSideEnabled);
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

	public static IdentityCardArtwork Artwork(StudentIdCardDto card, byte[]? photo, byte[]? logo)
	{
		var template = card.TemplateSnapshot;
		var (layout, _) = Resolve(template);
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
			"logo" => template.Logo.Mode != SchoolIdentityAssetMode.Hidden,
			"qr" => template.ShowQrCode,
			"barcode" => true,
			"photo" => template.ShowPhoto,
			_ => string.IsNullOrEmpty(element.FieldKey) || element.FieldKey is "schoolName" or "schoolAddress" or "headerText" or "footerText" or "static" or "CardNumber" || template.Fields.Contains(element.FieldKey)
		};
		string Render(bool back) => IdentityCardSvgRenderer.Render(layout, back, Value, Enabled, logo, photo, card.StudentId.ToString("N"), card.CardNumber);
		return new(Render(false), template.BackSideEnabled ? Render(true) : null);
	}
}
