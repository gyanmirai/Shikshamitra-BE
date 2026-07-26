using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmConfigurationReferenceKind
{
	Stage = 1,
	Round = 2,
	Exam = 3,
	SeatPlan = 4,
	Route = 5
}

public class AdmissionCrmConfigurationReferenceFamilyDto
{
	public string Family { get; set; } = string.Empty;
	public int ReferenceCount { get; set; }
	public string Description { get; set; } = string.Empty;
	public IReadOnlyList<Guid> SampleReferenceIds { get; set; } = [];
}

public class AdmissionCrmConfigurationReferenceResultDto
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmConfigurationReferenceKind Kind { get; set; }
	public string Code { get; set; } = string.Empty;
	public int TotalReferenceCount { get; set; }
	public IReadOnlyList<AdmissionCrmConfigurationReferenceFamilyDto> Families { get; set; } = [];
}

public class PreviewAdmissionCrmConfigurationImpactDto
{
	public AdmissionCrmConfigurationReferenceKind? Kind { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxPolicyCodeLength)]
	public string? Code { get; set; }

	public UpdateAdmissionCrmCycleConfigurationDto? ProposedCycleConfiguration { get; set; }
	public Guid? PipelineStageId { get; set; }
	public UpdateAdmissionCrmPipelineStageDto? ProposedPipelineStage { get; set; }
}

public class AdmissionCrmConfigurationImpactPreviewDto
{
	public Guid AcademicYearId { get; set; }
	public int AffectedApplicantCount { get; set; }
	public int ConfigurationReferenceCount { get; set; }
	public IReadOnlyList<string> ChangedFields { get; set; } = [];
	public IReadOnlyList<AdmissionCrmConfigurationReferenceFamilyDto> ReferenceFamilies { get; set; } = [];
	public IReadOnlyList<string> Warnings { get; set; } = [];
	public string Fingerprint { get; set; } = string.Empty;
}

public class PreviewAdmissionCrmConfigurationCloneDto
{
	public Guid SourceAcademicYearId { get; set; }
	public Guid TargetAcademicYearId { get; set; }
}

public class ConfirmAdmissionCrmConfigurationCloneDto : PreviewAdmissionCrmConfigurationCloneDto
{
	[Required]
	[MaxLength(64)]
	public string PreviewFingerprint { get; set; } = string.Empty;
}

public class AdmissionCrmConfigurationCloneFamilyDto
{
	public string Family { get; set; } = string.Empty;
	public int SourceCount { get; set; }
	public int CloneableCount { get; set; }
	public int TargetCount { get; set; }
	public string CloneBehavior { get; set; } = string.Empty;
}

public class AdmissionCrmConfigurationClonePreviewDto
{
	public Guid SourceAcademicYearId { get; set; }
	public Guid TargetAcademicYearId { get; set; }
	public bool CanConfirm { get; set; }
	public int TargetApplicantActivityCount { get; set; }
	public IReadOnlyList<AdmissionCrmConfigurationCloneFamilyDto> Families { get; set; } = [];
	public IReadOnlyList<string> Conflicts { get; set; } = [];
	public IReadOnlyList<string> Warnings { get; set; } = [];
	public string Fingerprint { get; set; } = string.Empty;
}

public class AdmissionCrmConfigurationCloneResultDto
{
	public Guid SourceAcademicYearId { get; set; }
	public Guid TargetAcademicYearId { get; set; }
	public bool AlreadyApplied { get; set; }
	public IReadOnlyList<AdmissionCrmConfigurationCloneFamilyDto> Families { get; set; } = [];
	public IReadOnlyList<string> Warnings { get; set; } = [];
	public string PreviewFingerprint { get; set; } = string.Empty;
}
