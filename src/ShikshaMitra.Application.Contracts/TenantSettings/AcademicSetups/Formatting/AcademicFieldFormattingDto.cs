using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public class AcademicFieldFormattingDto
{
	[Range(0, 0)]
	public AcademicTextCase Board { get; set; }
	[Range(0, 4)]
	public AcademicTextCase Medium { get; set; }
	[Range(0, 4)]
	public AcademicTextCase Stage { get; set; }
	[Range(0, 4)]
	public AcademicTextCase Standard { get; set; }
	[Range(0, 4)]
	public AcademicTextCase Division { get; set; }
}
