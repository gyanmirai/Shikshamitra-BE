namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public sealed record AcademicFieldFormattingRules
{
	public AcademicTextCase Board { get; init; }
	public AcademicTextCase Medium { get; init; }
	public AcademicTextCase Stage { get; init; }
	public AcademicTextCase Standard { get; init; }
	public AcademicTextCase Division { get; init; }
}
