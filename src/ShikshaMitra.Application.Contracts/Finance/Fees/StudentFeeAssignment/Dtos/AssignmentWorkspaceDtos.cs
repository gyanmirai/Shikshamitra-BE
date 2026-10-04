using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class AssignmentStudentListInput : PagedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string? Search { get; set; }
}

public class AssignmentTransportSourceDto
{
	public Guid Id { get; set; }
	public string Label { get; set; } = "";
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class AssignmentStudentDto
{
	public Guid StudentId { get; set; }
	public Guid EnrollmentId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string StudentName { get; set; } = "";
	public bool IsActive { get; set; }
	public bool IsFeeStructureAssigned { get; set; }
}

public class AssignmentCandidateDto
{
	public List<AssignmentPolicyDto> Policies { get; set; } = [];
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public int RevisionNumber { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public decimal Total { get; set; }
	public List<AssignmentHeadDto> Heads { get; set; } = [];
}

public class AssignmentServicePeriodDto
{
	public Guid FeeHeadId { get; set; }
	public string Source { get; set; } = "AcademicYear";
	public Guid? SourceReferenceId { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public string? Reason { get; set; }
}

public class AssignmentHeadDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = "";
	public bool IsOptional { get; set; }
	public bool IsOneTime { get; set; }
	public bool Included { get; set; }
	public decimal? ConfiguredAmount { get; set; }
	public decimal? EffectiveAmount { get; set; }
    public bool IsVariable { get; set; }
    public string? ZeroAmountReason { get; set; }
	public string AmountBasis { get; set; } = "AnnualTotal";
	public string? PaymentTermName { get; set; }
	public int? PaymentTermVersionNumber { get; set; }
	public bool SupportsTransportSource { get; set; }
	public Guid? PaymentTermId { get; set; }
	public Guid? PaymentTermVersionId { get; set; }
	public decimal Obligation { get; set; }
	public AssignmentServicePeriodDto? ServicePeriod { get; set; }
	public string Verification { get; set; } = "Configured academic year";
	public string? SourceRevision { get; set; }
}

public class AssignmentPolicyDto
{
	public string Source { get; set; } = "FeeStructure";
	public string Family { get; set; } = "";
	public Guid PolicyId { get; set; }
	public Guid VersionId { get; set; }
	public int Version { get; set; }
	public string Name { get; set; } = "";
	public string Description { get; set; } = "";
	public Dictionary<Guid, string> HeadTreatment { get; set; } = [];
}

public class AssignmentCommitResultDto
{
	public Guid AssignmentId { get; set; }
	public string Outcome { get; set; } = "Assigned";
	public StudentFeeAssignmentPreviewDto Assignment { get; set; } = new();
}

public class AssignmentBulkPreviewDto
{
	public List<AssignmentBulkPreviewItemDto> Results { get; set; } = [];
}

public class AssignmentBulkPreviewItemDto
{
	public Guid StudentId { get; set; }
	public StudentFeeAssignmentPreviewDto? Preview { get; set; }
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }
}
