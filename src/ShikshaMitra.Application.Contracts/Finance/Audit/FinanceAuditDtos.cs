using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Audit;

public class FinanceAuditEventListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string? SubModuleCode { get; set; }
	public string? EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public Guid? StudentId { get; set; }
	public string? Action { get; set; }
	public Guid? ActorUserId { get; set; }
	public string? CorrelationId { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public string? Search { get; set; }
}

public class FinanceAuditChangedFieldDto
{
	public string Field { get; set; } = string.Empty;
	public string? OldValue { get; set; }
	public string? NewValue { get; set; }
}

public class FinanceAuditEventDto : EntityDto<Guid>
{
	public Guid? AcademicYearId { get; set; }
	public string SubModuleCode { get; set; } = string.Empty;
	public string SubModuleDisplayName { get; set; } = string.Empty;
	public string EntityType { get; set; } = string.Empty;
	public Guid EntityId { get; set; }
	public string EntityName { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public string ActionLabel { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public Guid? StudentId { get; set; }
	public string? StudentNameSnapshot { get; set; }
	public string? StudentGrNoSnapshot { get; set; }
	public string? StudentRollNoSnapshot { get; set; }
	public Guid? ActorUserId { get; set; }
	public string? ActorNameSnapshot { get; set; }
	public string ActorDisplayName { get; set; } = string.Empty;
	public string? PerformedByRoleSnapshot { get; set; }
	public DateTime OccurredAt { get; set; }
	public DateTime OccurredAtUtc { get; set; }
	public string OccurredAtDisplay { get; set; } = string.Empty;
	public List<FinanceAuditChangedFieldDto> ChangedFields { get; set; } = [];
	public int ChangedFieldCount { get; set; }
	public string? ContextJson { get; set; }
	public string? CorrelationId { get; set; }
	public Guid? SourceAuditLogId { get; set; }
}

public class FinanceAuditCorrelationDto
{
	public string CorrelationId { get; set; } = string.Empty;
	public Guid? StudentId { get; set; }
	public string? StudentNameSnapshot { get; set; }
	public DateTime? OccurredAtFirst { get; set; }
	public DateTime? OccurredAtLast { get; set; }
	public int EventCount { get; set; }
	public List<FinanceAuditEventDto> Events { get; set; } = [];
}

public class FinanceAuditStudentContext
{
	public Guid? StudentId { get; set; }
	public string? StudentName { get; set; }
	public string? GrNo { get; set; }
	public string? RollNo { get; set; }
}

public class FinanceAuditWriteResult
{
	public Guid EventId { get; set; }
	public string CorrelationId { get; set; } = string.Empty;
}

public class FinanceAuditAllocationLineDto
{
	public Guid DueFeeId { get; set; }
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public string InstallmentLabel { get; set; } = string.Empty;
	public double AmountPaid { get; set; }
	public double PendingBefore { get; set; }
	public double PendingAfter { get; set; }
}

public class FinanceAuditSummaryDto
{
	public string Headline { get; set; } = string.Empty;
	public double TotalAmount { get; set; }
	public List<FinanceAuditChangedFieldDto> ChangedFields { get; set; } = [];
	public List<FinanceAuditAllocationLineDto> AllocationLines { get; set; } = [];
}

public class FinanceAuditStudentLookupRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string Search { get; set; } = string.Empty;
	public int MaxResultCount { get; set; } = 15;
}

public class FinanceAuditStudentLookupDto
{
	public Guid StudentId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string? GrNo { get; set; }
	public string? RollNo { get; set; }
	public string? StandardDivision { get; set; }
	public string Label { get; set; } = string.Empty;
}
