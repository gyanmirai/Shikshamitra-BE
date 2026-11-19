using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public class AcademicNamePreviewInput
{
	public const int MaxValueLength = 512;

	[Required(AllowEmptyStrings = true)]
	[StringLength(MaxValueLength)]
	public string? Value { get; set; }

	[Range(0, 4)]
	public AcademicTextCase TextCase { get; set; }
}
