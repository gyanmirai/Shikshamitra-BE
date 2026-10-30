using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormInheritancePolicyDto
{
	public bool Enabled { get; set; }
	public List<AdmissionFormInheritanceSourceDto> Sources { get; set; } = [];
}

public class AdmissionFormInheritanceSourceDto
{
	public Guid InquiryFormDefinitionId { get; set; }
	public List<string> RequiredFieldIdentities { get; set; } = [];
	public List<AdmissionFormInheritanceMappingDto> FieldMappings { get; set; } = [];
	public List<AdmissionFormInheritanceLayoutDto> Layouts { get; set; } = [];
}

public class AdmissionFormInheritanceMappingDto
{
	public string SourceFieldCode { get; set; } = string.Empty;
	public string RegistrationFieldCode { get; set; } = string.Empty;
}

public class AdmissionFormInheritanceLayoutDto
{
	public string FieldIdentity { get; set; } = string.Empty;
	public string? TargetSectionCode { get; set; }
	public int? DisplayOrder { get; set; }
}

public class AdmissionFormInheritedOriginDto
{
	public Guid InquiryFormDefinitionId { get; set; }
	public string SourceFieldCode { get; set; } = string.Empty;
	public string? SourceFieldIdentity { get; set; }
	public string? SourceBindingKey { get; set; }
	public string FieldIdentity { get; set; } = string.Empty;
	public bool SourceRequired { get; set; }
	public bool SourceReadOnly { get; set; }
}

public class AdmissionFormInheritancePreviewInput
{
	public Guid InquiryFormDefinitionId { get; set; }
	public Guid? RegistrationFormDefinitionId { get; set; }
	public CreateUpdateAdmissionFormDto? Draft { get; set; }
}

public class AdmissionFormInheritanceIssueDto
{
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? FieldIdentity { get; set; }
}

public class AdmissionFormInheritancePreviewDto
{
	public AdmissionFormDto Definition { get; set; } = new();
	public List<AdmissionFormSectionDto> InquiryContextSections { get; set; } = [];
	public Guid? InquiryFormDefinitionId { get; set; }
	public List<AdmissionFormInheritanceIssueDto> Issues { get; set; } = [];
	public bool IsValid => Issues.Count == 0;
}

public class AdmissionFormInheritanceConnectionDto
{
	public Guid OfferingId { get; set; }
	public string OfferingName { get; set; } = string.Empty;
}

public class AdmissionFormInheritanceSourceSummaryDto
{
	public Guid InquiryFormDefinitionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public bool IsApproved { get; set; }
	public List<AdmissionFormInheritanceConnectionDto> Connections { get; set; } = [];
}
