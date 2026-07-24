using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Audit;

public class AcademicAuditEventListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public string? SubModuleCode { get; set; }
	public string? EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public string? Action { get; set; }
	public Guid? ActorUserId { get; set; }
	public string? CorrelationId { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public string? Search { get; set; }
}

public class AcademicAuditChangedFieldDto
{
	public string Field { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string? OldValue { get; set; }
	public string? NewValue { get; set; }
	public string ChangeType { get; set; } = "updated";
}

public class AcademicAuditEventDto : EntityDto<Guid>
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
	public Guid? ActorUserId { get; set; }
	public string? ActorNameSnapshot { get; set; }
	public string ActorDisplayName { get; set; } = string.Empty;
	public string? ActorRoleSnapshot { get; set; }
	public DateTime OccurredAt { get; set; }
	public DateTime OccurredAtUtc { get; set; }
	public string OccurredAtDisplay { get; set; } = string.Empty;
	public List<AcademicAuditChangedFieldDto> ChangedFields { get; set; } = [];
	public int ChangedFieldCount { get; set; }
	public string? ContextJson { get; set; }
	public string? CorrelationId { get; set; }
	public Guid? SourceAuditLogId { get; set; }
	public string Source { get; set; } = "academic";
}
