using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class AdmissionFormPrintAdvancedSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintPaperSettingsDto? Paper { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintBorderSettingsDto? Border { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintStyleSettingsDto? Style { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintBrandingSettingsDto? Branding { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintHeaderSettingsDto? Header { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintPhotoSettingsDto? Photo { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintFooterSettingsDto? Footer { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintFlowSettingsDto? Flow { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, AdmissionPrintSectionSettingsDto?>? Sections { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, AdmissionPrintFieldSettingsDto?>? Fields { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintContentSettingsDto? Content { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintCopiesSettingsDto? Copies { get; set; }
}

public sealed class AdmissionPrintPaperSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Orientation { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintMarginsDto? Margins { get; set; }
}

public sealed class AdmissionPrintMarginsDto
{
	public double Top { get; set; }
	public double Right { get; set; }
	public double Bottom { get; set; }
	public double Left { get; set; }
}

public sealed class AdmissionPrintBorderSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Enabled { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Placement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Style { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Thickness { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Inset { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Corners { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? SectionBorders { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? FieldBorders { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Dividers { get; set; }
}

public sealed class AdmissionPrintStyleSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Palette { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SectionFill { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BorderColor { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TextColor { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FontFamily { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? BaseSize { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? TitleSize { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LabelWeight { get; set; }
}

public sealed class AdmissionPrintAssetPlacementDto
{
	public double X { get; set; }
	public double Y { get; set; }
	public double Width { get; set; }
	public double Height { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? LockAspectRatio { get; set; }
}

public sealed class AdmissionPrintBrandingSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowLogo { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowBanner { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BannerPlacement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<int>? BannerPages { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintAssetPlacementDto? Logo { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AdmissionPrintAssetPlacementDto? Banner { get; set; }
}

public sealed class AdmissionPrintHeaderSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Continuation { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? SchoolName { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Address { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Phone { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Email { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Website { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Board { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Medium { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Class { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? AcademicYear { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Title { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Subtitle { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Alignment { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? RegistrationNumber { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Date { get; set; }
}

public sealed class AdmissionPrintPhotoSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Placement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Alignment { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Width { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Height { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Caption { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? UseSaved { get; set; }
}

public sealed class AdmissionPrintFooterSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Placement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Alignment { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? PageNumbers { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? RegistrationReference { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? SchoolName { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? FormTitle { get; set; }
}

public sealed class AdmissionPrintFlowSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? StepTitles { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? SectionNumbers { get; set; }
}

public sealed class AdmissionPrintSectionSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Columns { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Spacing { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? FullWidth { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? StartNewPage { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? KeepHeading { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? RepeatTableHeadings { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BlankRows { get; set; }
}

public sealed class AdmissionPrintFieldSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Width { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LabelPosition { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AnswerStyle { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? WritingLines { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowRequired { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowHelp { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowConditional { get; set; }
}

public sealed class AdmissionPrintContentSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowInstructions { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InstructionsPlacement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InstructionSection { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowChecklist { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ChecklistPlacement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowDeclaration { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SignaturePlacement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ParentSignature { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? OfficeSignature { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ParentSignatureImage { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OfficeSignatureImage { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? SignatureDate { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? ShowOfficeUse { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OfficePlacement { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? DocumentChecks { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Remarks { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? VerificationDate { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Stamp { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, AdmissionPrintContentTranslationDto?>? Translations { get; set; }
}

public sealed class AdmissionPrintContentTranslationDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Instructions { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string?>? Checklist { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Declaration { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ParentLabel { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OfficeLabel { get; set; }
}

public sealed class AdmissionPrintCopiesSettingsDto
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Unanswered { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SelectedChoices { get; set; }
}

