using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class BulkWaiverPreviewRequestDto
{
	public Guid AcademicYearId { get; set; }
	/// <summary>"Class" | "Section" | "FeeStructure" | "WholeSchool"</summary>
	public string Scope { get; set; } = "WholeSchool";
	public Guid? ScopeId { get; set; }
}

public class BulkWaiverPreviewResponseDto
{
	public int AffectedStudentCount { get; set; }
	public int AffectedDueLineCount { get; set; }
	public decimal AffectedAmount { get; set; }
}

public class CreateBulkWaiverRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string Scope { get; set; } = "WholeSchool";
	public Guid? ScopeId { get; set; }
	public string ReasonCode { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}

public class BulkWaiverRequestDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Scope { get; set; } = "WholeSchool";
	public Guid? ScopeId { get; set; }
	public string ReasonCode { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public Guid RequestedByUserId { get; set; }
	public string Status { get; set; } = "PendingApproval";
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public DateTime? AppliedAt { get; set; }
	public int PreviewAffectedStudentCount { get; set; }
	public int PreviewAffectedDueLineCount { get; set; }
	public decimal PreviewAffectedAmount { get; set; }
	public int AffectedDueLineCount { get; set; }
	public DateTime CreationTime { get; set; }
}

public class BulkWaiverApplyResultDto
{
	public Guid BulkWaiverRequestId { get; set; }
	public int WaiversCreated { get; set; }
	public decimal TotalWaivedAmount { get; set; }
}

public class BulkWaiverRollupItemDto
{
	public Guid LateFeeWaiverApplicationId { get; set; }
	public Guid StudentId { get; set; }
	public Guid DueFeeId { get; set; }
	public decimal EffectiveAmount { get; set; }
}
