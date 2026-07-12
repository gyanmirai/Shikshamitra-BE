using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFormFieldDto
{
	[Required]
	[MaxLength(80)]
	public required string Key { get; set; }

	[Required]
	[MaxLength(120)]
	public required string Label { get; set; }

	public AdmissionCrmFormFieldType FieldType { get; set; } = AdmissionCrmFormFieldType.Text;

	[MaxLength(120)]
	public string? SystemField { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxFormSectionNameLength)]
	public string? SectionName { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxFormHelperTextLength)]
	public string? HelperText { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxFormPlaceholderLength)]
	public string? Placeholder { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxFormDefaultValueLength)]
	public string? DefaultValue { get; set; }

	[MaxLength(80)]
	public string? VisibleWhenFieldKey { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxFormVisibilityValueLength)]
	public string? VisibleWhenValue { get; set; }

	public bool Required { get; set; }
	public int DisplayOrder { get; set; }
	public IReadOnlyList<string> Options { get; set; } = [];
	public IReadOnlyList<string> OptionLabels { get; set; } = [];
	public IReadOnlyDictionary<string, string> LocalizedLabels { get; set; } = new Dictionary<string, string>();
	public IReadOnlyDictionary<string, string> LocalizedHelperTexts { get; set; } = new Dictionary<string, string>();
	public IReadOnlyDictionary<string, string> LocalizedPlaceholders { get; set; } = new Dictionary<string, string>();
	public IReadOnlyDictionary<string, IReadOnlyList<string>> LocalizedOptions { get; set; } = new Dictionary<string, IReadOnlyList<string>>();
}
