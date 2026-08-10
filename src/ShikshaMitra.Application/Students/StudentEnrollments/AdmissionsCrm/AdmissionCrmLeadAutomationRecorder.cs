using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadAutomationRecorder(
	IRepository<AdmissionCrmAutomationRun, Guid> repository) : ApplicationService, ITransientDependency
{
	internal async Task RecordStaleArchiveAutomationRunAsync(
		Guid academicYearId,
		DateTimeOffset startedAt,
		AdmissionCrmStaleArchiveRunResultDto result)
	{
		await repository.InsertAsync(
			new AdmissionCrmAutomationRun(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				AdmissionCrmAutomationRunType.StaleArchive,
				AdmissionCrmAutomationRunStatus.Completed,
				startedAt,
				new DateTimeOffset(Clock.Now),
				$"Marked {result.MarkedStaleCount} lead(s) stale, archived {result.ArchivedCount}, created {result.CreatedOwnerNotificationTaskCount} owner task(s), skipped {result.SkippedExistingOwnerNotificationTaskCount}.",
				createdTaskCount: result.CreatedOwnerNotificationTaskCount,
				skippedExistingTaskCount: result.SkippedExistingOwnerNotificationTaskCount,
				markedStaleCount: result.MarkedStaleCount,
				archivedCount: result.ArchivedCount),
			autoSave: true);
	}

	internal async Task RecordSlaReminderAutomationRunAsync(
		Guid academicYearId,
		DateTimeOffset startedAt,
		AdmissionCrmSlaReminderRunResultDto result)
	{
		await repository.InsertAsync(
			new AdmissionCrmAutomationRun(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				AdmissionCrmAutomationRunType.SlaReminder,
				AdmissionCrmAutomationRunStatus.Completed,
				startedAt,
				new DateTimeOffset(Clock.Now),
				$"SLA default threshold {result.SlaThresholdHours} hour(s), stage overrides {result.StageOverrideCount}, cutoff {result.SlaCutoff.UtcDateTime:O}, created {result.CreatedTaskCount} task(s), skipped {result.SkippedExistingTaskCount}.",
				createdTaskCount: result.CreatedTaskCount,
				skippedExistingTaskCount: result.SkippedExistingTaskCount),
			autoSave: true);
	}

	internal async Task RecordJourneyAutomationRunAsync(
		Guid academicYearId,
		DateTimeOffset startedAt,
		AdmissionCrmJourneyRunResultDto result)
	{
		var status = result.FailedActionCount > 0
			? AdmissionCrmAutomationRunStatus.CompletedWithFailures
			: AdmissionCrmAutomationRunStatus.Completed;
		await repository.InsertAsync(
			new AdmissionCrmAutomationRun(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				AdmissionCrmAutomationRunType.Journey,
				status,
				startedAt,
				new DateTimeOffset(Clock.Now),
				$"Journey {result.Trigger} evaluated {result.EvaluatedLeadCount} lead(s), matched {result.MatchedRuleCount} rule action(s), created {result.CreatedTaskCount} task(s), sent {result.SentMessageCount} message(s), failed {result.FailedActionCount}.",
				result.Trigger,
				result.Stage,
				result.LeadId,
				result.EvaluatedLeadCount,
				result.MatchedRuleCount,
				result.CreatedTaskCount,
				result.SentMessageCount,
				result.SkippedExistingTaskCount,
				result.SkippedExistingMessageCount,
				result.FailedActionCount),
			autoSave: true);
	}
}