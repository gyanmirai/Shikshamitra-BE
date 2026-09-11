using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionInquiryManagementAppService(
	AdmissionInquiryScopeAccessService access,
	AdmissionInquiryRuntimeService runtime,
	AdmissionInquiryAssignmentService assignments,
	IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionInquiryManagement, Guid> management,
	IRepository<AdmissionInquiryFollowUp, Guid> followUps,
	IRepository<AdmissionActivity, Guid> activities) : ApplicationService, IAdmissionInquiryManagementAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<AdmissionInquiryManagementDto> GetAsync(Guid inquiryId)
	{
		var inquiry = await access.GetAsync(inquiryId);
		return await MapAsync(inquiry, await runtime.EnsureManagementAsync(inquiry));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> RecordContactAsync(Guid inquiryId, RecordAdmissionInquiryContactDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		var contactedAt = input.ContactedAt?.UtcDateTime ?? Clock.Now;
		if (contactedAt > Clock.Now.AddMinutes(5) || contactedAt < inquiry.CreationTime.AddDays(-1))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose a valid contact time.");
		state.RecordContact(contactedAt);
		inquiry.MarkContacted(contactedAt);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryContactRecorded", "Inquiry contact recorded",
			new { input.Note, ContactedAt = contactedAt, Actor = CurrentUser.Id });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid inquiryId, ScheduleAdmissionInquiryFollowUpDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		var scheduledAt = input.ScheduledAt.UtcDateTime;
		if (scheduledAt <= Clock.Now || scheduledAt > Clock.Now.AddDays(365))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Schedule the follow-up within the next year.");
		var open = await followUps.CountAsync(item => item.InquiryId == inquiry.Id && item.Status == AdmissionInquiryFollowUpStatus.Scheduled);
		if (open >= policy.MaxOpenFollowUps)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.FollowUpLimitReached,
				$"This policy allows up to {policy.MaxOpenFollowUps} open follow-ups.");
		var assignee = input.AssignedCounsellorId ?? inquiry.AssignedCounsellorId;
		if (assignee.HasValue)
			await assignments.EnsureValidAsync(assignee.Value,
				inquiry.AdmissionOfferingId ?? throw new BusinessException(AdmissionErrorCodes.InquiryOfferingRequired));
		if (policy.RestrictActionsToAssignedCounsellor && !assignee.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired);
		var followUp = await followUps.InsertAsync(new AdmissionInquiryFollowUp(GuidGenerator.Create(), CurrentTenant.Id,
			inquiry.Id, state.Id, assignee, scheduledAt, input.Purpose), false);
		state.Touch();
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryFollowUpScheduled", "Inquiry follow-up scheduled",
			new { followUp.Id, followUp.ScheduledAt, followUp.Purpose, followUp.AssignedCounsellorId });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CompleteFollowUpAsync(Guid inquiryId, Guid followUpId,
		CompleteAdmissionInquiryFollowUpDto input) => FinishFollowUpAsync(inquiryId, followUpId, input, false);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public Task<AdmissionInquiryManagementDto> CancelFollowUpAsync(Guid inquiryId, Guid followUpId,
		CancelAdmissionInquiryFollowUpDto input) => FinishFollowUpAsync(inquiryId, followUpId, input, true);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> QualifyAsync(Guid inquiryId, QualifyAdmissionInquiryDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		ValidateAnswers(policy, input);
		if (input.Outcome == AdmissionInquiryQualificationOutcome.FollowUp &&
			!await followUps.AnyAsync(item => item.InquiryId == inquiry.Id && item.Status == AdmissionInquiryFollowUpStatus.Scheduled))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.QualificationRequired,
				"Schedule the next follow-up before selecting Follow-up required.");
		if (input.Outcome == AdmissionInquiryQualificationOutcome.Duplicate)
			await EnsureDuplicateTargetAsync(inquiry, input.DuplicateOfId);
		else if (input.DuplicateOfId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		var actor = CurrentUser.Id ?? throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		var answersJson = JsonSerializer.Serialize(input.Answers.Select(answer => new
		{
			Code = answer.Code.Trim().ToUpperInvariant(), answer.Passed, Notes = answer.Notes?.Trim()
		}), JsonOptions);
		state.RecordOutcome(input.Outcome, answersJson, input.Reason, actor, Clock.Now);
		inquiry.SetStatus(MapStatus(input.Outcome), input.DuplicateOfId);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryQualificationRecorded", $"Inquiry outcome recorded: {input.Outcome}",
			new { input.Outcome, input.Reason, input.DuplicateOfId, Answers = input.Answers, PolicyVersion = policy.Version, Actor = actor });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> ReopenAsync(Guid inquiryId, ReopenAdmissionInquiryDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		state.Reopen();
		inquiry.SetStatus(AdmissionInquiryStatus.FollowUp);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryQualificationReopened", "Inquiry qualification reopened",
			new { input.Reason, Actor = CurrentUser.Id });
		return await MapAsync(inquiry, state);
	}

	private async Task<AdmissionInquiryManagementDto> FinishFollowUpAsync(Guid inquiryId, Guid followUpId,
		CompleteAdmissionInquiryFollowUpDto input, bool cancel)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		var followUp = await followUps.FindAsync(item => item.Id == followUpId && item.InquiryId == inquiry.Id && item.ManagementId == state.Id) ??
			throw new EntityNotFoundException(typeof(AdmissionInquiryFollowUp), followUpId);
		if (!string.Equals(followUp.ConcurrencyStamp, input.FollowUpConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		var actor = CurrentUser.Id ?? throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		if (cancel) followUp.Cancel(actor, Clock.Now, input.Note); else followUp.Complete(actor, Clock.Now, input.Note);
		await followUps.UpdateAsync(followUp, false);
		state.Touch();
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, cancel ? "InquiryFollowUpCancelled" : "InquiryFollowUpCompleted",
			cancel ? "Inquiry follow-up cancelled" : "Inquiry follow-up completed",
			new { followUp.Id, input.Note, Actor = actor });
		return await MapAsync(inquiry, state);
	}

	private async Task<(AdmissionInquiry Inquiry, AdmissionInquiryManagement State, AdmissionInquiryPolicy Policy)> GetMutableAsync(
		Guid inquiryId, string concurrencyStamp)
	{
		var inquiry = await access.GetAsync(inquiryId);
		if (inquiry.Status is AdmissionInquiryStatus.ApplicationStarted or AdmissionInquiryStatus.Converted)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState,
				"Inquiry management is locked after the application has started.");
		var state = await runtime.EnsureManagementAsync(inquiry) ??
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState,
				"Enable managed Inquiry settings before using this action.");
		if (string.IsNullOrWhiteSpace(concurrencyStamp) || !string.Equals(state.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		return (inquiry, state, AdmissionInquiryPolicyResolver.ReadSnapshot(state.PolicySnapshotJson));
	}

	private void EnsureActor(AdmissionInquiryPolicy policy, AdmissionInquiry inquiry)
	{
		if (!policy.RestrictActionsToAssignedCounsellor) return;
		if (!CurrentUser.Id.HasValue || !inquiry.AssignedCounsellorId.HasValue)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.CounsellorRequired);
		if (CurrentUser.Id.Value != inquiry.AssignedCounsellorId.Value)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState,
				"Only the assigned counsellor can perform this action.");
	}

	private static void ValidateAnswers(AdmissionInquiryPolicy policy, QualifyAdmissionInquiryDto input)
	{
		if (!Enum.IsDefined(input.Outcome) || string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		var answers = input.Answers.GroupBy(answer => answer.Code.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
		if (answers.Any(group => group.Count() != 1) || answers.Any(group =>
			!policy.QualificationChecklist.Any(item => string.Equals(item.Code, group.Key, StringComparison.OrdinalIgnoreCase))))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Use each configured qualification item once.");
		if (input.Outcome == AdmissionInquiryQualificationOutcome.Qualified && policy.QualificationChecklist
			.Where(item => item.Required).Any(item => !answers.Any(group =>
				string.Equals(group.Key, item.Code, StringComparison.OrdinalIgnoreCase) && group.Single().Passed)))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ChecklistRequired,
				"Complete every required qualification item before qualifying the inquiry.");
	}

	private async Task EnsureDuplicateTargetAsync(AdmissionInquiry inquiry, Guid? duplicateOfId)
	{
		if (!duplicateOfId.HasValue || duplicateOfId == inquiry.Id)
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateTargetInvalid);
		var target = await access.GetAsync(duplicateOfId.Value);
		if (target.AdmissionCycleId != inquiry.AdmissionCycleId)
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateTargetInvalid);
	}

	private async Task<AdmissionInquiryManagementDto> MapAsync(AdmissionInquiry inquiry, AdmissionInquiryManagement? state)
	{
		if (state == null) return new AdmissionInquiryManagementDto { InquiryId = inquiry.Id };
		var policy = AdmissionInquiryPolicyResolver.ReadSnapshot(state.PolicySnapshotJson);
		var blockers = AdmissionInquiryRuntimeService.GetReadinessBlockers(inquiry, state, policy);
		var isLocked = inquiry.Status is AdmissionInquiryStatus.ApplicationStarted or AdmissionInquiryStatus.Converted;
		return new AdmissionInquiryManagementDto
		{
			InquiryId = inquiry.Id, Managed = true, Policy = policy, ConcurrencyStamp = state.ConcurrencyStamp,
			AssignedCounsellorId = inquiry.AssignedCounsellorId, FirstContactDueAt = state.FirstContactDueAt,
			QualificationDueAt = state.QualificationDueAt, LastContactAt = state.LastContactAt,
			ContactCount = state.ContactCount, Outcome = state.Outcome, OutcomeReason = state.OutcomeReason,
			OutcomeAt = state.OutcomeAt, OutcomeBy = state.OutcomeBy,
			IsOverdue = AdmissionInquiryRuntimeService.IsOverdue(state, Clock.Now), IsLocked = isLocked,
			CanStartApplication = !isLocked && blockers.Count == 0,
			Blockers = blockers, QualificationAnswers = ReadAnswers(state.QualificationAnswersJson),
			FollowUps = (await followUps.GetListAsync(item => item.InquiryId == inquiry.Id)).OrderBy(item => item.ScheduledAt).Select(Map).ToList()
		};
	}

	private async Task AddActivityAsync(AdmissionInquiry inquiry, string type, string description, object metadata)
	{
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, type, description,
			inquiry.ApplicantId, inquiryId: inquiry.Id, metadataJson: JsonSerializer.Serialize(metadata, JsonOptions)), true);
	}

	private static List<AdmissionInquiryQualificationAnswerDto> ReadAnswers(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try { return JsonSerializer.Deserialize<List<AdmissionInquiryQualificationAnswerDto>>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []; }
		catch (JsonException) { return []; }
	}

	private static AdmissionInquiryFollowUpDto Map(AdmissionInquiryFollowUp item) => new()
	{
		Id = item.Id, AssignedCounsellorId = item.AssignedCounsellorId, ScheduledAt = item.ScheduledAt,
		Purpose = item.Purpose, Status = item.Status, CompletedAt = item.CompletedAt, CompletedBy = item.CompletedBy,
		CompletionNote = item.CompletionNote, ConcurrencyStamp = item.ConcurrencyStamp
	};

	private static AdmissionInquiryStatus MapStatus(AdmissionInquiryQualificationOutcome outcome) => outcome switch
	{
		AdmissionInquiryQualificationOutcome.Qualified => AdmissionInquiryStatus.Interested,
		AdmissionInquiryQualificationOutcome.FollowUp => AdmissionInquiryStatus.FollowUp,
		AdmissionInquiryQualificationOutcome.NotInterested => AdmissionInquiryStatus.NotInterested,
		AdmissionInquiryQualificationOutcome.Lost => AdmissionInquiryStatus.Lost,
		AdmissionInquiryQualificationOutcome.Duplicate => AdmissionInquiryStatus.Duplicate,
		_ => throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput)
	};
}
