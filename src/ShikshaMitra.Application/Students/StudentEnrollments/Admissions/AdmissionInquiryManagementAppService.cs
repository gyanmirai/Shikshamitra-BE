using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionInquiryManagementAppService : ApplicationService, IAdmissionInquiryManagementAppService
{
	private readonly AdmissionInquiryScopeAccessService access;
	private readonly AdmissionInquiryRuntimeService runtime;
	private readonly AdmissionOfferingRuntimeService offeringRuntime;
	private readonly AdmissionInquiryAvailabilityService availability;
	private readonly IRepository<AdmissionInquiry, Guid> inquiries;
	private readonly IRepository<AdmissionInquiryManagement, Guid> management;
	private readonly IRepository<AdmissionInquiryFollowUp, Guid> followUps;
	private readonly IRepository<AdmissionActivity, Guid> activities;

	public AdmissionInquiryManagementAppService(
		AdmissionInquiryScopeAccessService access,
		AdmissionInquiryRuntimeService runtime,
		AdmissionInquiryAssignmentService assignments,
		AdmissionOfferingRuntimeService offeringRuntime,
		AdmissionInquiryAvailabilityService availability,
		IRepository<AdmissionInquiry, Guid> inquiries,
		IRepository<AdmissionInquiryManagement, Guid> management,
		IRepository<AdmissionInquiryFollowUp, Guid> followUps,
		IRepository<AdmissionActivity, Guid> activities
	)
	{
		this.access = access;
		this.runtime = runtime;
		_ = assignments;
		this.offeringRuntime = offeringRuntime;
		this.availability = availability;
		this.inquiries = inquiries;
		this.management = management;
		this.followUps = followUps;
		this.activities = activities;
	}

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
		ValidateContact(input);
		if (contactedAt > Clock.Now.AddMinutes(5) || contactedAt < inquiry.CreationTime.AddDays(-1))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose a valid contact time.");
		state.RecordContact(contactedAt);
		inquiry.MarkContacted(contactedAt);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryContactRecorded",
			$"{DisplayCode(input.Direction)} {DisplayCode(input.Channel)} contact: {DisplayCode(input.Outcome)}",
			new { input.Channel, input.Direction, input.Outcome, input.Note, ContactedAt = contactedAt, Actor = CurrentUser.Id });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	[UnitOfWork(true)]
	public async Task<AdmissionInquiryManagementDto> RecordFollowUpNoteAsync(Guid inquiryId, RecordAdmissionInquiryFollowUpNoteDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		if (string.IsNullOrWhiteSpace(input.InquiryConcurrencyStamp) || !string.Equals(inquiry.ConcurrencyStamp, input.InquiryConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		var actor = CurrentUser.Id ?? throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState);
		var note = input.Note.Trim();
		AdmissionInquiryRuntimeService.EnsureValidStatusChange(inquiry, input.Status, note);
		var reminderAt = input.Status == AdmissionInquiryStatus.NotInterested ? null : input.ReminderAt;
		Guid? assignee = null;
		if (input.Status != AdmissionInquiryStatus.NotInterested)
		{
			if (!reminderAt.HasValue)
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose the next follow-up reminder.");
			assignee = await ValidateScheduleAsync(inquiry, policy, reminderAt.Value.UtcDateTime, null, replacingReminder: true, utcNow: GetUtcNow());
		}
		var previousReminders = await followUps.GetListAsync(item => item.InquiryId == inquiry.Id && item.Status == AdmissionInquiryFollowUpStatus.Scheduled);
		foreach (var previous in previousReminders)
		{
			var reason = input.Status == AdmissionInquiryStatus.NotInterested
				? "Reminder cleared because the inquiry was marked Not interested."
				: "Reminder superseded by the next follow-up note.";
			previous.Cancel(actor, Clock.Now, reason);
			await followUps.UpdateAsync(previous, false);
			await AddActivityAsync(inquiry, "InquiryFollowUpCancelled", reason,
				new { previous.Id, Note = reason, Actor = actor, SystemGenerated = true }, autoSave: false);
		}
		await runtime.UpdateStatusAsync(inquiry, input.Status, note);
		await inquiries.UpdateAsync(inquiry, false);
		Guid? followUpId = reminderAt.HasValue
			? await InsertFollowUpAsync(inquiry, state, assignee, reminderAt.Value.UtcDateTime, note)
			: null;
		state.Touch();
		await management.UpdateAsync(state, false);
		var authorName = string.Join(" ", new[] { CurrentUser.Name, CurrentUser.SurName }.Where(value => !string.IsNullOrWhiteSpace(value)));
		if (string.IsNullOrWhiteSpace(authorName)) authorName = CurrentUser.UserName ?? "Staff";
		await AddActivityAsync(inquiry, "InquiryFollowUpNoteRecorded", "Inquiry follow-up note recorded",
			new { Note = note, input.Status, ReminderAt = reminderAt, FollowUpId = followUpId, AuthorName = authorName, Actor = actor });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid inquiryId, ScheduleAdmissionInquiryFollowUpDto input)
		=> await ScheduleFollowUpCoreAsync(inquiryId, input);

	private async Task<AdmissionInquiryManagementDto> ScheduleFollowUpCoreAsync(Guid inquiryId, ScheduleAdmissionInquiryFollowUpDto input, AdmissionApplication? application = null)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp, application);
		EnsureActor(policy, inquiry);
		var scheduledAt = input.ScheduledAt.UtcDateTime;
		var utcNow = application == null ? (DateTime?)null : GetUtcNow();
		var assignee = await ValidateScheduleAsync(inquiry, policy, scheduledAt, input.AssignedCounsellorId, utcNow: utcNow);
		state.Touch();
		await management.UpdateAsync(state, false);
		await InsertFollowUpAsync(inquiry, state, assignee, scheduledAt, input.Purpose, application?.Id);
		return await MapFollowUpResultAsync(inquiry, state, application?.Id);
	}

	private async Task<Guid?> ValidateScheduleAsync(AdmissionInquiry inquiry, AdmissionInquiryPolicy policy, DateTime scheduledAt, Guid? requestedAssignee, bool replacingReminder = false, DateTime? utcNow = null)
	{
		var now = utcNow ?? Clock.Now;
		if (scheduledAt <= now || scheduledAt > now.AddDays(365))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Schedule the follow-up within the next year.");
		if (!replacingReminder)
		{
			var open = await followUps.CountAsync(item => item.InquiryId == inquiry.Id && item.Status == AdmissionInquiryFollowUpStatus.Scheduled);
			if (open >= policy.MaxOpenFollowUps)
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.FollowUpLimitReached,
				$"This policy allows up to {policy.MaxOpenFollowUps} open follow-ups.");
		}
		return null;
	}

	private async Task<Guid> InsertFollowUpAsync(AdmissionInquiry inquiry, AdmissionInquiryManagement state, Guid? assignee, DateTime scheduledAt, string purpose, Guid? applicationId = null)
	{
		var followUp = await followUps.InsertAsync(new AdmissionInquiryFollowUp(GuidGenerator.Create(), CurrentTenant.Id,
			inquiry.Id, state.Id, assignee, scheduledAt, purpose), false);
		await AddActivityAsync(inquiry, "InquiryFollowUpScheduled", "Inquiry follow-up scheduled",
			new { followUp.Id, followUp.ScheduledAt, followUp.Purpose, followUp.AssignedCounsellorId }, applicationId);
		return followUp.Id;
	}

	[RemoteService(false)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<AdmissionInquiryManagementDto> GetApplicationFollowUpsAsync(Guid applicationId)
	{
		var application = await GetLinkedApplicationAsync(applicationId);
		var inquiry = await access.GetAsync(application.InquiryId!.Value);
		var state = await runtime.EnsureManagementAsync(inquiry);
		var result = await MapAsync(inquiry, state);
		result.CanManageFollowUps = state != null && await IsOpenApplicationAsync(application) &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage) &&
			(!result.Policy!.RestrictActionsToAssignedCounsellor || inquiry.AssignedCounsellorId == CurrentUser.Id && CurrentUser.Id.HasValue);
		NormalizeApplicationFollowUpDates(result, state);
		return result;
	}

	[RemoteService(false)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> ScheduleApplicationFollowUpAsync(Guid applicationId, ScheduleAdmissionInquiryFollowUpDto input)
	{
		var application = await GetLinkedApplicationAsync(applicationId);
		return await ScheduleFollowUpCoreAsync(application.InquiryId!.Value, input, application);
	}

	[RemoteService(false)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> CompleteApplicationFollowUpAsync(Guid applicationId, Guid followUpId, CompleteAdmissionInquiryFollowUpDto input)
	{
		var application = await GetLinkedApplicationAsync(applicationId);
		return await FinishFollowUpAsync(application.InquiryId!.Value, followUpId, input, false, application);
	}

	[RemoteService(false)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> CancelApplicationFollowUpAsync(Guid applicationId, Guid followUpId, CancelAdmissionInquiryFollowUpDto input)
	{
		var application = await GetLinkedApplicationAsync(applicationId);
		return await FinishFollowUpAsync(application.InquiryId!.Value, followUpId, input, true, application);
	}

	private async Task<AdmissionApplication> GetLinkedApplicationAsync(Guid applicationId)
	{
		var application = await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationReadService>().GetEntityAsync(applicationId);
		if (!application.InquiryId.HasValue) throw new EntityNotFoundException(typeof(AdmissionInquiry), applicationId);
		if (!await inquiries.AnyAsync(item => item.Id == application.InquiryId.Value))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState, "The source inquiry is unavailable. Continue admission work in the application; follow-ups require its linked inquiry.")
				.WithData("Reason", "SourceInquiryUnavailable");
		return application;
	}

	private async Task<bool> IsOpenApplicationAsync(AdmissionApplication application) => application.Status is not
		(AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.Rejected or AdmissionApplicationStatus.OfferDeclined or
		AdmissionApplicationStatus.Enrolled or AdmissionApplicationStatus.AdmissionFinalized) &&
		!await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().AnyAsync(item =>
			item.ApplicationId == application.Id && (item.Status == AdmissionWorkflowInstanceStatus.Cancelled || item.Status == AdmissionWorkflowInstanceStatus.Failed));

	private async Task<AdmissionInquiryManagementDto> MapFollowUpResultAsync(AdmissionInquiry inquiry, AdmissionInquiryManagement state, Guid? applicationId)
	{
		var result = await MapAsync(inquiry, state);
		if (applicationId.HasValue)
		{
			result.CanManageFollowUps = true; // Both actor and active application have been validated by the command.
			NormalizeApplicationFollowUpDates(result, state);
		}
		return result;
	}

	private void NormalizeApplicationFollowUpDates(AdmissionInquiryManagementDto result, AdmissionInquiryManagement? state)
	{
		// SQL timestamps can lose DateTime.Kind; the application API must emit their authoritative UTC offset.
		result.IsOverdue = state != null && AdmissionInquiryRuntimeService.IsOverdue(state, GetUtcNow());
		result.FirstContactDueAt = NormalizeUtc(result.FirstContactDueAt);
		result.QualificationDueAt = NormalizeUtc(result.QualificationDueAt);
		result.LastContactAt = NormalizeUtc(result.LastContactAt);
		result.OutcomeAt = NormalizeUtc(result.OutcomeAt);
		foreach (var followUp in result.FollowUps)
		{
			followUp.ScheduledAt = AdmissionOfferUtcTime.Normalize(followUp.ScheduledAt);
			followUp.CompletedAt = NormalizeUtc(followUp.CompletedAt);
		}
		foreach (var note in result.Notes) note.OccurredAt = AdmissionOfferUtcTime.Normalize(note.OccurredAt);
	}

	private static DateTime? NormalizeUtc(DateTime? value) => value.HasValue ? AdmissionOfferUtcTime.Normalize(value.Value) : null;

	private DateTime GetUtcNow() => NormalizeFollowUpClockNow(Clock.Now, Clock.Kind);

	internal static DateTime NormalizeFollowUpClockNow(DateTime now, DateTimeKind configuredKind) =>
		configuredKind == DateTimeKind.Utc ? AdmissionOfferUtcTime.Normalize(now) : now.ToUniversalTime();

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
		if (input.Outcome == AdmissionInquiryQualificationOutcome.Qualified && inquiry.ApplicantId.HasValue)
			AdmissionPrimaryContactSelection.EnsureReady(await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplicant, Guid>>().GetAsync(inquiry.ApplicantId.Value));
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
		var reasonCode = NormalizeReasonCode(input.Outcome, input.ReasonCode);
		state.RecordOutcome(input.Outcome, answersJson, reasonCode, input.Reason, actor, Clock.Now);
		inquiry.SetStatus(MapStatus(input.Outcome), input.DuplicateOfId);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		var qualificationDescription = reasonCode == null
			? $"Inquiry outcome recorded: {DisplayCode(input.Outcome.ToString())}"
			: $"Inquiry outcome recorded: {DisplayCode(input.Outcome.ToString())} ({DisplayCode(reasonCode)})";
		await AddActivityAsync(inquiry, "InquiryQualificationRecorded", qualificationDescription,
			new { input.Outcome, ReasonCode = reasonCode, input.Reason, input.DuplicateOfId, Answers = input.Answers, PolicyVersion = policy.Version, Actor = actor });
		return await MapAsync(inquiry, state);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryManagementDto> ReopenAsync(Guid inquiryId, ReopenAdmissionInquiryDto input)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp);
		EnsureActor(policy, inquiry);
		state.Reopen();
		inquiry.SetStatus(AdmissionInquiryStatus.New);
		await inquiries.UpdateAsync(inquiry, false);
		await management.UpdateAsync(state, false);
		await AddActivityAsync(inquiry, "InquiryQualificationReopened", "Inquiry qualification reopened",
			new { input.Reason, Actor = CurrentUser.Id });
		return await MapAsync(inquiry, state);
	}

	private async Task<AdmissionInquiryManagementDto> FinishFollowUpAsync(Guid inquiryId, Guid followUpId,
		CompleteAdmissionInquiryFollowUpDto input, bool cancel, AdmissionApplication? application = null)
	{
		var (inquiry, state, policy) = await GetMutableAsync(inquiryId, input.ConcurrencyStamp, application);
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
			new { followUp.Id, input.Note, Actor = actor }, application?.Id);
		return await MapFollowUpResultAsync(inquiry, state, application?.Id);
	}

	private async Task<(AdmissionInquiry Inquiry, AdmissionInquiryManagement State, AdmissionInquiryPolicy Policy)> GetMutableAsync(
		Guid inquiryId, string concurrencyStamp, AdmissionApplication? application = null)
	{
		var inquiry = await access.GetAsync(inquiryId);
		if (application != null)
		{
			if (application.InquiryId != inquiry.Id || !await IsOpenApplicationAsync(application))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidState, "This admission is closed. Follow-ups cannot be changed.");
		}
		else if (AdmissionInquiryStatusRules.IsTransferred(inquiry.Status) || await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().AnyAsync(application => application.InquiryId == inquiry.Id))
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
		// Action permission and academic scope are enforced by the owning command.
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
		var isLocked = AdmissionInquiryStatusRules.IsTransferred(inquiry.Status) ||
			await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().AnyAsync(application => application.InquiryId == inquiry.Id);
		var blockers = new List<string>();
		AdmissionOffering? offering = null;
		if (isLocked) blockers.Add("This inquiry has already moved to Pipeline.");
		if (inquiry.Status != AdmissionInquiryStatus.Interested) blockers.Add("Mark the inquiry Interested before moving to the next stage.");
		if (!inquiry.AdmissionOfferingId.HasValue) blockers.Add("Resolve the admission offering before moving this legacy inquiry.");
		if (!isLocked && inquiry.AdmissionOfferingId.HasValue)
		{
			try { offering = (await offeringRuntime.GetAvailableAsync(inquiry.AdmissionOfferingId.Value)).Offering; await availability.EnsureAvailableAsync(offering); }
			catch (BusinessException exception) { blockers.Add(exception.Message); }
		}
		if (!isLocked && offering == null && inquiry.AdmissionOfferingId.HasValue)
			offering = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().FindAsync(inquiry.AdmissionOfferingId.Value);
		var canReassign = !isLocked && offering?.CounsellorAssignmentProfileId.HasValue == true && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign) && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		var emailStatus = inquiry.GetProperty<string>("QualificationEmailStatus") ?? (inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt").HasValue ? "Pending" : null);
		var emailMessage = inquiry.GetProperty<string>("QualificationEmailMessage");
		var inquiryActivities = await activities.GetListAsync(item => item.InquiryId == inquiry.Id);
		var notes = inquiryActivities.OrderByDescending(item => item.OccurredAt)
			.Select(ReadNote).Where(item => item != null).Select(item => item!).ToList();
		if (state == null) return new AdmissionInquiryManagementDto { InquiryId = inquiry.Id, IsLocked = isLocked, CanStartApplication = blockers.Count == 0, CanReassign = canReassign, Blockers = blockers, QualificationEmailStatus = emailStatus, QualificationEmailMessage = emailMessage, Notes = notes };
		var policy = AdmissionInquiryPolicyResolver.ReadSnapshot(state.PolicySnapshotJson);
		var projectedFollowUps = (await followUps.GetListAsync(item => item.InquiryId == inquiry.Id)).OrderBy(item => item.ScheduledAt).Select(Map).ToList();
		return new AdmissionInquiryManagementDto
		{
			InquiryId = inquiry.Id, Managed = true, Policy = policy, ConcurrencyStamp = state.ConcurrencyStamp,
			AssignedCounsellorId = inquiry.AssignedCounsellorId, FirstContactDueAt = state.FirstContactDueAt,
			QualificationDueAt = state.QualificationDueAt, LastContactAt = state.LastContactAt,
			NextReminderAt = FindNextReminderAt(projectedFollowUps, inquiryActivities),
			ContactCount = state.ContactCount, Outcome = state.Outcome, OutcomeReason = state.OutcomeReason,
			OutcomeReasonCode = state.OutcomeReasonCode,
			OutcomeAt = state.OutcomeAt, OutcomeBy = state.OutcomeBy,
			IsOverdue = AdmissionInquiryRuntimeService.IsOverdue(state, Clock.Now), IsLocked = isLocked,
			CanStartApplication = blockers.Count == 0, CanReassign = canReassign,
			QualificationEmailStatus = emailStatus, QualificationEmailMessage = emailMessage,
			Blockers = blockers, QualificationAnswers = ReadAnswers(state.QualificationAnswersJson),
			FollowUps = projectedFollowUps,
			Notes = notes
		};
	}

	private async Task AddActivityAsync(AdmissionInquiry inquiry, string type, string description, object metadata, Guid? applicationId = null, bool autoSave = true)
	{
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(type, description,
			inquiry.ApplicantId, applicationId, inquiryId: inquiry.Id, metadataJson: JsonSerializer.Serialize(metadata, JsonOptions)), autoSave);
	}

	private static List<AdmissionInquiryQualificationAnswerDto> ReadAnswers(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try { return JsonSerializer.Deserialize<List<AdmissionInquiryQualificationAnswerDto>>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []; }
		catch (JsonException) { return []; }
	}

	private static AdmissionInquiryFollowUpNoteDto? ReadNote(AdmissionActivity activity)
	{
		var noteKey = activity.ActivityType switch
		{
			"InquiryFollowUpNoteRecorded" or "InquiryContactRecorded" or "InquiryFollowUpCompleted" or "InquiryFollowUpCancelled" => "note",
			"InquiryQualificationRecorded" or "InquiryQualificationReopened" => "reason",
			_ => null
		};
		if (noteKey == null || string.IsNullOrWhiteSpace(activity.MetadataJson)) return null;
		try
		{
			using var document = JsonDocument.Parse(activity.MetadataJson);
			var metadata = document.RootElement;
			if (metadata.ValueKind != JsonValueKind.Object || metadata.TryGetProperty("systemGenerated", out var generated) && generated.ValueKind == JsonValueKind.True) return null;
			if (!metadata.TryGetProperty(noteKey, out var note) || note.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(note.GetString())) return null;
			AdmissionInquiryStatus? status = null;
			DateTimeOffset? reminderAt = null;
			if (activity.ActivityType == "InquiryFollowUpNoteRecorded")
			{
				if (metadata.TryGetProperty("status", out var savedStatus) && savedStatus.ValueKind == JsonValueKind.Number && savedStatus.TryGetInt32(out var number) && AdmissionInquiryStatusRules.IsBusinessStatus((AdmissionInquiryStatus)number))
					status = (AdmissionInquiryStatus)number;
				if (metadata.TryGetProperty("reminderAt", out var reminder) && reminder.ValueKind == JsonValueKind.String && reminder.TryGetDateTimeOffset(out var time)) reminderAt = time;
			}
			var authorName = metadata.TryGetProperty("authorName", out var author) && author.ValueKind == JsonValueKind.String ? author.GetString() : null;
			return new AdmissionInquiryFollowUpNoteDto
			{
				Id = activity.Id, OccurredAt = activity.OccurredAt, Note = note.GetString()!, Status = status,
				ReminderAt = reminderAt, AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Staff" : authorName
			};
		}
		catch (JsonException) { return null; }
	}

	internal static DateTimeOffset? FindNextReminderAt(IEnumerable<AdmissionInquiryFollowUpDto> tasks, IEnumerable<AdmissionActivity>? history = null)
	{
		var originalInstants = new Dictionary<Guid, DateTimeOffset>();
		foreach (var activity in (history ?? []).OrderBy(item => item.OccurredAt))
		{
			var isNote = activity.ActivityType == "InquiryFollowUpNoteRecorded";
			if (!isNote && activity.ActivityType != "InquiryFollowUpScheduled" || string.IsNullOrWhiteSpace(activity.MetadataJson)) continue;
			try
			{
				using var document = JsonDocument.Parse(activity.MetadataJson);
				var metadata = document.RootElement;
				if (metadata.ValueKind != JsonValueKind.Object ||
					!metadata.TryGetProperty(isNote ? "followUpId" : "id", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var taskId) ||
					!metadata.TryGetProperty(isNote ? "reminderAt" : "scheduledAt", out var saved) || saved.ValueKind != JsonValueKind.String) continue;
				var text = saved.GetString()!;
				// Older timezone-less snapshots do not prove an absolute instant.
				if (!(text.EndsWith('Z') || text.LastIndexOf('+') > 10 || text.LastIndexOf('-') > 10) || !saved.TryGetDateTimeOffset(out var instant)) continue;
				originalInstants[taskId] = instant.ToUniversalTime();
			}
			catch (JsonException) { }
		}
		return tasks.Where(item => item.Status == AdmissionInquiryFollowUpStatus.Scheduled)
			.Select(item => (DateTimeOffset?)(originalInstants.TryGetValue(item.Id, out var instant)
				? instant : new DateTimeOffset(AdmissionOfferUtcTime.Normalize(item.ScheduledAt))))
			.OrderBy(instant => instant).FirstOrDefault();
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
		AdmissionInquiryQualificationOutcome.FollowUp => AdmissionInquiryStatus.New,
		AdmissionInquiryQualificationOutcome.NotInterested => AdmissionInquiryStatus.NotInterested,
		AdmissionInquiryQualificationOutcome.Lost => AdmissionInquiryStatus.NotInterested,
		AdmissionInquiryQualificationOutcome.Duplicate => AdmissionInquiryStatus.Duplicate,
		_ => throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput)
	};

	private static string? NormalizeReasonCode(AdmissionInquiryQualificationOutcome outcome, string? reasonCode)
	{
		if (outcome is not (AdmissionInquiryQualificationOutcome.NotInterested or
			AdmissionInquiryQualificationOutcome.Lost or AdmissionInquiryQualificationOutcome.Duplicate))
			return null;
		var normalized = reasonCode?.Trim().ToUpperInvariant();
		var allowed = new[]
		{
			"TIMING", "BUDGET", "NO_RESPONSE", "CHANGED_PLAN", "JOINED_OTHER_SCHOOL",
			"SEAT_UNAVAILABLE", "FEES", "LOCATION", "ACADEMIC_FIT", "DUPLICATE", "OTHER"
		};
		if (string.IsNullOrWhiteSpace(normalized) || !allowed.Contains(normalized))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid closure reason.");
		return normalized;
	}

	private static void ValidateContact(RecordAdmissionInquiryContactDto input)
	{
		var channels = new[] { "PHONE", "WHATSAPP", "SMS", "EMAIL", "IN_PERSON", "VIDEO" };
		var directions = new[] { "OUTBOUND", "INBOUND" };
		var outcomes = new[] { "CONNECTED", "NO_ANSWER", "CALL_BACK", "INFORMATION_SHARED", "VISIT_PLANNED", "OTHER" };
		if (!channels.Contains(input.Channel.Trim().ToUpperInvariant()) ||
			!directions.Contains(input.Direction.Trim().ToUpperInvariant()) ||
			!outcomes.Contains(input.Outcome.Trim().ToUpperInvariant()))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Choose a valid contact channel, direction, and outcome.");
	}

	private static string DisplayCode(string value) => string.Join(" ", value.Trim()
		.Split('_', StringSplitOptions.RemoveEmptyEntries)
		.Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
}
