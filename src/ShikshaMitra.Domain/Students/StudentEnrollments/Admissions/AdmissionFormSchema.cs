using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormSchema
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int Version { get; set; }
	public AdmissionFormStatus Status { get; set; }
	public Guid? ParentVersionId { get; set; }
	public DateTime? PublishedAt { get; set; }
	public Guid? PublishedBy { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public AdmissionFormLayoutType LayoutType { get; set; }
	public AdmissionFormPurpose Purpose { get; set; }
	public int FieldsPerRow { get; set; } = 1;
	public string DefaultLanguage { get; set; } = "en";
	public List<string> SupportedLanguages { get; set; } = ["en"];
	public List<AdmissionFormSchemaSection> Sections { get; set; } = [];
}

public class AdmissionFormSchemaSection
{
	public Guid Id { get; set; }
	public Guid FormDefinitionId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public Guid? StepId { get; set; }
	public string? StepCode { get; set; }
	public string? StepName { get; set; }
	public int? StepDisplayOrder { get; set; }
	public string? StepVisibilityRuleJson { get; set; }
	public bool IsRepeatable { get; set; }
	public int MinEntries { get; set; } = 1;
	public int MaxEntries { get; set; } = 1;
	public string? EntryLabel { get; set; }
	public Dictionary<string, AdmissionFormSchemaTranslation> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<AdmissionFormSchemaField> Fields { get; set; } = [];
}

public class AdmissionFormSchemaField
{
	public Guid Id { get; set; }
	public Guid FormDefinitionId { get; set; }
	public Guid SectionId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public AdmissionFormFieldType FieldType { get; set; }
	public AdmissionFormBindingType BindingType { get; set; }
	public string? BindingKey { get; set; }
	public string? FieldIdentity { get; set; }
	public AdmissionFormInheritedOrigin? InheritedFrom { get; set; }
	public string? Placeholder { get; set; }
	public string? HelpText { get; set; }
	public string? DefaultValue { get; set; }
	public string? OptionsJson { get; set; }
	public bool Required { get; set; }
	public bool ReadOnly { get; set; }
	public int DisplayOrder { get; set; }
	public string? ValidationJson { get; set; }
	public string? VisibilityRuleJson { get; set; }
	public string? RequiredRuleJson { get; set; }
	public Dictionary<string, AdmissionFormSchemaTranslation> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AdmissionFormInheritedOrigin
{
	public Guid InquiryFormDefinitionId { get; set; }
	public string SourceFieldCode { get; set; } = string.Empty;
	public string? SourceFieldIdentity { get; set; }
	public string? SourceBindingKey { get; set; }
	public string FieldIdentity { get; set; } = string.Empty;
	public bool SourceRequired { get; set; }
	public bool SourceReadOnly { get; set; }
}

public class AdmissionFormSchemaTranslation
{
	public string? Label { get; set; }
	public string? Placeholder { get; set; }
	public string? HelpText { get; set; }
	public string? ValidationMessage { get; set; }
	public Dictionary<string, string> Choices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AdmissionFormInheritanceIssue
{
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? FieldIdentity { get; set; }
}

public class AdmissionFormInheritanceComposition
{
	public AdmissionFormSchema Definition { get; set; } = new();
	public List<AdmissionFormSchemaSection> InquiryContextSections { get; set; } = [];
	public List<AdmissionFormInheritanceIssue> Issues { get; set; } = [];
}
