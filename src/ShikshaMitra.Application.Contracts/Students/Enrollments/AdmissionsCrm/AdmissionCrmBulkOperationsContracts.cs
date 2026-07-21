using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmBulkLeadOwnerItemDto
{
	public Guid LeadId { get; set; }
	public Guid? OwnerUserId { get; set; }
}

public class AdmissionCrmBulkAssignOwnersDto
{
	public IReadOnlyList<AdmissionCrmBulkLeadOwnerItemDto> Items { get; set; } = [];
}

public class AdmissionCrmBulkCreateTasksDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public string Title { get; set; } = string.Empty;
	public DateTime DueAt { get; set; }
	public Guid? AssignedUserId { get; set; }
	public string? Notes { get; set; }
}

public class AdmissionCrmBulkExamAttendanceDto
{
	public IReadOnlyList<Guid> ExamRecordIds { get; set; } = [];
	public AdmissionCrmExamAttendanceStatus AttendanceStatus { get; set; }
}

public class AdmissionCrmBulkChangeStageDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public AdmissionInquiryStage Stage { get; set; }
	public string Reason { get; set; } = string.Empty;
	public bool OverrideProtectedStages { get; set; }
}

public class AdmissionCrmBulkArchiveDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmBulkSendMessageDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public Guid TemplateId { get; set; }
	public Dictionary<string, string> Variables { get; set; } = [];
}

public class AdmissionCrmBulkOfferDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public CreateAdmissionCrmSeatOfferDto Offer { get; set; } = new();
}

public class AdmissionCrmBulkActivationPreviewDto
{
	public IReadOnlyList<Guid> LeadIds { get; set; } = [];
	public PreviewAdmissionCrmActivationDto Activation { get; set; } = new();
}

public class AdmissionCrmBulkExecuteActivationsDto
{
	public IReadOnlyList<Guid> ActivationIds { get; set; } = [];
}

public class AdmissionCrmBulkOperationItemResultDto
{
	public Guid EntityId { get; set; }
	public bool Success { get; set; }
	public string? ResultId { get; set; }
	public string? ErrorCode { get; set; }
	public string? Message { get; set; }
	public IReadOnlyList<string> BlockingReasons { get; set; } = [];
}

public class AdmissionCrmBulkOperationResultDto
{
	public int RequestedCount { get; set; }
	public int SucceededCount { get; set; }
	public int FailedCount { get; set; }
	public IReadOnlyList<AdmissionCrmBulkOperationItemResultDto> Items { get; set; } = [];
}
