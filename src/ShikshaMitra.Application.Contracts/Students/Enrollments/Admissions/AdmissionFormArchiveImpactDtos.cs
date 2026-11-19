using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormArchiveImpactDto
{
	public Guid FormDefinitionId { get; set; }
	public string FormName { get; set; } = string.Empty;
	public AdmissionFormPurpose Purpose { get; set; }
	public AdmissionFormStatus Status { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public int InquiryCount { get; set; }
	public int ApplicationCount { get; set; }
	public int OfferingCount { get; set; }
	public int WorkflowCount { get; set; }
	public int PublicLinkCount { get; set; }
	public bool HasRestrictedOfferings { get; set; }
	public bool HasRestrictedDependencies { get; set; }
	public List<AdmissionFormArchiveDependencyDto> Dependencies { get; set; } = [];
}

public class AdmissionFormArchiveDependencyDto
{
	public string Kind { get; set; } = string.Empty;
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid? WorkflowStageId { get; set; }
	public string? WorkflowStageName { get; set; }
	public bool? IsActive { get; set; }
}
