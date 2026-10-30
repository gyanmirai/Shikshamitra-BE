using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormInheritancePolicy
{
	public bool Enabled { get; set; }
	public List<AdmissionFormInheritanceSource> Sources { get; set; } = [];
}

public class AdmissionFormInheritanceSource
{
	public Guid InquiryFormDefinitionId { get; set; }
	public List<string> RequiredFieldIdentities { get; set; } = [];
	public List<AdmissionFormInheritanceMapping> FieldMappings { get; set; } = [];
	public List<AdmissionFormInheritanceLayout> Layouts { get; set; } = [];
}

public class AdmissionFormInheritanceMapping
{
	public string SourceFieldCode { get; set; } = string.Empty;
	public string RegistrationFieldCode { get; set; } = string.Empty;
}

public class AdmissionFormInheritanceLayout
{
	public string FieldIdentity { get; set; } = string.Empty;
	public string? TargetSectionCode { get; set; }
	public int? DisplayOrder { get; set; }
}
