using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAutomationRun : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public AdmissionCrmAutomationRunType RunType { get; private set; }
	public AdmissionCrmAutomationRunStatus Status { get; private set; }
	public AdmissionCrmJourneyTrigger? Trigger { get; private set; }
	public AdmissionInquiryStage? Stage { get; private set; }
	public Guid? LeadId { get; private set; }
	public DateTimeOffset StartedAt { get; private set; }
	public DateTimeOffset CompletedAt { get; private set; }
	public int EvaluatedLeadCount { get; private set; }
	public int MatchedRuleCount { get; private set; }
	public int CreatedTaskCount { get; private set; }
	public int SentMessageCount { get; private set; }
	public int SkippedExistingTaskCount { get; private set; }
	public int SkippedExistingMessageCount { get; private set; }
	public int FailedActionCount { get; private set; }
	public int MarkedStaleCount { get; private set; }
	public int ArchivedCount { get; private set; }
	public string? Details { get; private set; }

	protected AdmissionCrmAutomationRun()
	{
	}

	public AdmissionCrmAutomationRun(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		AdmissionCrmAutomationRunType runType,
		AdmissionCrmAutomationRunStatus status,
		DateTimeOffset startedAt,
		DateTimeOffset completedAt,
		string? details = null,
		AdmissionCrmJourneyTrigger? trigger = null,
		AdmissionInquiryStage? stage = null,
		Guid? leadId = null,
		int evaluatedLeadCount = 0,
		int matchedRuleCount = 0,
		int createdTaskCount = 0,
		int sentMessageCount = 0,
		int skippedExistingTaskCount = 0,
		int skippedExistingMessageCount = 0,
		int failedActionCount = 0,
		int markedStaleCount = 0,
		int archivedCount = 0)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		RunType = runType;
		Status = status;
		Trigger = trigger;
		Stage = stage;
		LeadId = leadId;
		StartedAt = startedAt;
		CompletedAt = completedAt;
		EvaluatedLeadCount = evaluatedLeadCount;
		MatchedRuleCount = matchedRuleCount;
		CreatedTaskCount = createdTaskCount;
		SentMessageCount = sentMessageCount;
		SkippedExistingTaskCount = skippedExistingTaskCount;
		SkippedExistingMessageCount = skippedExistingMessageCount;
		FailedActionCount = failedActionCount;
		MarkedStaleCount = markedStaleCount;
		ArchivedCount = archivedCount;
		Details = NormalizeOptional(details, nameof(details), AdmissionCrmConsts.MaxTimelineDetailsLength);
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
