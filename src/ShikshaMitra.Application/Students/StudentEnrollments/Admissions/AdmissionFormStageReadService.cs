using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormStageReadService(
	IRepository<AdmissionFormSectionDefinition, Guid> sections,
	IRepository<AdmissionFormSubmission, Guid> submissions,
	IRepository<AdmissionFormChangeHistory, Guid> history,
	AdmissionFormStageAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionFormStageDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var policy = context.Policy;
		var attempts = await submissions.GetListAsync(item => item.FormInstanceId == context.Form.Id);
		var formSections = (await sections.GetListAsync(item => item.FormDefinitionId == context.Form.FormDefinitionId))
			.OrderBy(item => item.DisplayOrder).ToList();
		var events = (await history.GetListAsync(item => item.FormInstanceId == context.Form.Id))
			.OrderBy(item => item.OccurredAt).ToList();
		var lastCorrection = events.LastOrDefault(item => item.ChangeType == "CorrectionRequested")?.OccurredAt;
		var verificationEvents = events.Where(item => item.ChangeType == "SectionVerification" &&
			(!lastCorrection.HasValue || item.OccurredAt > lastCorrection.Value));
		var latestByCode = verificationEvents.Select(ReadVerificationEvent).Where(item => item != null)
			.Cast<VerificationEvent>().GroupBy(item => item.SectionCode, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
		var requiredCodes = (policy?.VerificationSectionCodes ?? [])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var sectionDtos = formSections.Select(section =>
		{
			latestByCode.TryGetValue(section.Code, out var latest);
			return new AdmissionFormSectionVerificationDto
			{
				SectionId = section.Id,
				SectionCode = section.Code,
				SectionName = section.Name,
				Required = requiredCodes.Contains(section.Code),
				Verified = latest?.Verified == true,
				VerifiedBy = latest?.ChangedBy,
				VerifiedAt = latest?.OccurredAt,
				Reason = latest?.Reason
			};
		}).ToList();
		var dueAt = context.DueAt;
		var overdue = dueAt.HasValue && dueAt.Value < Clock.Now && context.Form.Status != AdmissionFormInstanceStatus.Submitted;
		var blockers = BuildBlockers(context, attempts.Count, overdue, sectionDtos);
		var attemptAvailable = policy == null || attempts.Count < policy.MaxSubmissionAttempts;
		var deadlineAllowsSubmit = policy == null || policy.AllowLateSubmission || !overdue;
		return new AdmissionFormStageDto
		{
			ApplicationId = applicationId,
			StageId = stageId,
			FormStageEnabled = policy != null,
			Policy = policy,
			IsActive = context.IsActive,
			FormStatus = context.Form.Status,
			DueAt = dueAt,
			IsOverdue = overdue,
			SubmissionAttempts = attempts.Count,
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			CanSaveDraft = context.CanPrepare && context.Form.Status != AdmissionFormInstanceStatus.Submitted &&
				(policy?.AllowSaveAndResume ?? true),
			CanSubmit = context.IsActive && context.Application.Status == AdmissionApplicationStatus.Draft &&
				context.Form.Status != AdmissionFormInstanceStatus.Submitted && attemptAvailable && deadlineAllowsSubmit,
			CanVerify = context.CanPrepare && policy?.RequireSectionVerification == true &&
				context.Form.Status == AdmissionFormInstanceStatus.Submitted,
			CanRequestCorrection = context.CanPrepare && policy?.AllowCorrectionRequests == true &&
				context.Form.Status == AdmissionFormInstanceStatus.Submitted && attemptAvailable,
			CanComplete = context.IsActive && policy != null && blockers.Count == 0,
			Blockers = blockers,
			Sections = sectionDtos
		};
	}

	private static List<string> BuildBlockers(
		AdmissionFormStageContext context,
		int attempts,
		bool overdue,
		IEnumerable<AdmissionFormSectionVerificationDto> sections)
	{
		var policy = context.Policy;
		if (policy == null) return [];
		var blockers = new List<string>();
		if (context.Form.Status != AdmissionFormInstanceStatus.Submitted)
			blockers.Add("Submit the application form.");
		if (attempts >= policy.MaxSubmissionAttempts && context.Form.Status != AdmissionFormInstanceStatus.Submitted)
			blockers.Add($"The limit of {policy.MaxSubmissionAttempts} submission attempts has been reached.");
		if (overdue && !policy.AllowLateSubmission)
			blockers.Add("The form submission deadline has passed.");
		foreach (var section in sections.Where(item => item.Required && !item.Verified))
			blockers.Add($"Verify the {section.SectionName} section.");
		return blockers;
	}

	private static VerificationEvent? ReadVerificationEvent(AdmissionFormChangeHistory item)
	{
		if (string.IsNullOrWhiteSpace(item.ChangedFieldsJson)) return null;
		try
		{
			var value = JsonSerializer.Deserialize<VerificationPayload>(item.ChangedFieldsJson,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			return string.IsNullOrWhiteSpace(value?.SectionCode) ? null :
				new VerificationEvent(value.SectionCode, value.Verified, value.Reason, item.ChangedBy, item.OccurredAt);
		}
		catch (JsonException) { return null; }
	}

	private sealed record VerificationPayload(string SectionCode, bool Verified, string? Reason);
	private sealed record VerificationEvent(string SectionCode, bool Verified, string? Reason, Guid? ChangedBy, DateTime OccurredAt);
}
