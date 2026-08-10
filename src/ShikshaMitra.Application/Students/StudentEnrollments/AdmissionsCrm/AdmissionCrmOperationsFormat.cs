using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmOperationsFormat
{
	internal static AdmissionCrmMyWorkItemDto CreateTaskWorkItem(
		AdmissionCrmFollowUpTask task,
		AdmissionInquiryLead lead,
		Guid assignedUserId,
		DateTime now)
	{
		var dueAt = ToOffset(task.DueAt);
		return new AdmissionCrmMyWorkItemDto
		{
			Key = $"task:{task.Id}",
			WorkType = AdmissionCrmMyWorkType.Task,
			TaskId = task.Id,
			LeadId = lead.Id,
			ApplicantNumber = lead.ApplicantNumber,
			ApplicantName = GetApplicantName(lead),
			Contact = lead.PrimaryContactNo ?? lead.Email,
			TargetStandardId = lead.TargetStandardId,
			TargetClassName = lead.DesiredClassName,
			Stage = lead.Stage,
			Priority = lead.Priority,
			Title = task.Title,
			Details = task.Notes,
			DueAt = dueAt,
			Urgency = GetUrgency(dueAt, now),
			IsUrgent = false,
			OwnerUserId = lead.OwnerUserId,
			AssignedUserId = assignedUserId,
			RecommendedAction = AdmissionCrmRecommendedAction.CompleteTask,
			RecommendedSection = GetSectionForStage(lead.Stage),
			AllowedActions = [AdmissionCrmRecommendedAction.CompleteTask, AdmissionCrmRecommendedAction.OpenApplicant]
		};
	}

	internal static AdmissionCrmMyWorkItemDto CreateBlockerWorkItem(
		AdmissionCrmOperationsQueueType queueType,
		AdmissionCrmOperationsQueueItemDto item,
		AdmissionInquiryLead lead,
		Guid assignedUserId,
		DateTime now)
	{
		var action = GetRecommendedAction(queueType, item);
		return new AdmissionCrmMyWorkItemDto
		{
			Key = $"queue:{(int) queueType}:{item.Key}",
			WorkType = AdmissionCrmMyWorkType.OperationalBlocker,
			QueueType = queueType,
			LeadId = lead.Id,
			ApplicantNumber = lead.ApplicantNumber,
			ExamRecordId = item.ExamRecordId,
			ApplicantName = item.ApplicantName,
			Contact = lead.PrimaryContactNo ?? lead.Email,
			TargetStandardId = lead.TargetStandardId,
			TargetClassName = lead.DesiredClassName,
			Stage = lead.Stage,
			Priority = lead.Priority,
			Title = item.Title,
			Details = item.Details,
			DueAt = item.DueAt,
			Urgency = GetUrgency(item.DueAt, now),
			IsUrgent = item.IsUrgent,
			OwnerUserId = lead.OwnerUserId,
			AssignedUserId = assignedUserId,
			RecommendedAction = action,
			RecommendedSection = GetSectionForQueue(queueType),
			AllowedActions = action == AdmissionCrmRecommendedAction.RescheduleExam
				? [AdmissionCrmRecommendedAction.RescheduleExam, AdmissionCrmRecommendedAction.DecideEligibility, AdmissionCrmRecommendedAction.OpenApplicant]
				: [action, AdmissionCrmRecommendedAction.OpenApplicant]
		};
	}

	internal static AdmissionCrmRecommendedAction GetRecommendedAction(
		AdmissionCrmOperationsQueueType queueType,
		AdmissionCrmOperationsQueueItemDto item) => queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => AdmissionCrmRecommendedAction.ScheduleExam,
			AdmissionCrmOperationsQueueType.NoShow when item.Status == AdmissionCrmExamAttendanceStatus.Absent.ToString() ||
															item.Status == AdmissionCrmExamAttendanceStatus.NoShow.ToString() => AdmissionCrmRecommendedAction.RescheduleExam,
			AdmissionCrmOperationsQueueType.NoShow => AdmissionCrmRecommendedAction.RecordAttendance,
			AdmissionCrmOperationsQueueType.CapacityConflict => AdmissionCrmRecommendedAction.RescheduleExam,
			AdmissionCrmOperationsQueueType.RegistrationInvitationPending or
			AdmissionCrmOperationsQueueType.PendingPaperEntry or
			AdmissionCrmOperationsQueueType.ExpiringRegistrationLink or
			AdmissionCrmOperationsQueueType.IncompleteRegistration => AdmissionCrmRecommendedAction.ReviewRegistration,
			AdmissionCrmOperationsQueueType.RejectedDocument or
			AdmissionCrmOperationsQueueType.DocumentVerificationBacklog => AdmissionCrmRecommendedAction.ReviewDocuments,
			AdmissionCrmOperationsQueueType.ActivationRepair or
			AdmissionCrmOperationsQueueType.ServiceHandoffException => AdmissionCrmRecommendedAction.RepairActivation,
			AdmissionCrmOperationsQueueType.FinalApproval or
			AdmissionCrmOperationsQueueType.PolicyException or
			AdmissionCrmOperationsQueueType.PaymentCondition => AdmissionCrmRecommendedAction.ReviewOffer,
			_ => AdmissionCrmRecommendedAction.OpenApplicant
		};

	internal static AdmissionCrmWorkspaceSection GetSectionForQueue(AdmissionCrmOperationsQueueType queueType)
	{
		if (AdmissionCrmOperationsQueueBuilder.IsExamQueue(queueType)) return AdmissionCrmWorkspaceSection.EntranceExam;
		if (AdmissionCrmOperationsQueueBuilder.IsRegistrationQueue(queueType)) return AdmissionCrmWorkspaceSection.Registration;
		if (AdmissionCrmOperationsQueueBuilder.IsOfferQueue(queueType)) return AdmissionCrmWorkspaceSection.Offer;
		if (AdmissionCrmOperationsQueueBuilder.IsActivationQueue(queueType)) return AdmissionCrmWorkspaceSection.Activation;
		return AdmissionCrmWorkspaceSection.Governance;
	}

	internal static AdmissionCrmWorkspaceSection GetSectionForStage(AdmissionInquiryStage stage)
	{
		if (stage is AdmissionInquiryStage.ExamReady or AdmissionInquiryStage.ExamScheduled or AdmissionInquiryStage.ExamCompleted)
			return AdmissionCrmWorkspaceSection.EntranceExam;
		return AdmissionCrmWorkspaceSection.Overview;
	}

	internal static AdmissionCrmWorkUrgency GetUrgency(DateTimeOffset? dueAt, DateTime now)
	{
		if (!dueAt.HasValue) return AdmissionCrmWorkUrgency.Normal;
		var dueDate = dueAt.Value.UtcDateTime.Date;
		var today = now.ToUniversalTime().Date;
		if (dueDate < today) return AdmissionCrmWorkUrgency.Overdue;
		if (dueDate == today) return AdmissionCrmWorkUrgency.DueToday;
		if (dueDate <= today.AddDays(3)) return AdmissionCrmWorkUrgency.DueSoon;
		return AdmissionCrmWorkUrgency.Normal;
	}

	internal static IEnumerable<AdmissionCrmMyWorkItemDto> ApplyMyWorkSorting(
		IEnumerable<AdmissionCrmMyWorkItemDto> items,
		string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"applicantnumber" => descending ? items.OrderByDescending(x => x.ApplicantNumber) : items.OrderBy(x => x.ApplicantNumber),
			"applicantname" => descending ? items.OrderByDescending(x => x.ApplicantName) : items.OrderBy(x => x.ApplicantName),
			"priority" => descending
				? items.OrderByDescending(x => GetPriorityRank(x.Priority))
				: items.OrderBy(x => GetPriorityRank(x.Priority)),
			"dueat" => descending
				? items.OrderByDescending(x => x.DueAt ?? DateTimeOffset.MinValue)
				: items.OrderBy(x => x.DueAt ?? DateTimeOffset.MaxValue),
			_ => items.OrderByDescending(x => x.Urgency)
				.ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
				.ThenBy(x => GetPriorityRank(x.Priority))
				.ThenBy(x => x.ApplicantName)
		};
	}

	internal static IEnumerable<AdmissionCrmOperationsQueueItemDto> ApplyQueueSorting(
		IEnumerable<AdmissionCrmOperationsQueueItemDto> items,
		string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"applicantnumber" => descending ? items.OrderByDescending(x => x.ApplicantNumber) : items.OrderBy(x => x.ApplicantNumber),
			"applicantname" => descending ? items.OrderByDescending(x => x.ApplicantName) : items.OrderBy(x => x.ApplicantName),
			"title" => descending ? items.OrderByDescending(x => x.Title) : items.OrderBy(x => x.Title),
			"status" => descending ? items.OrderByDescending(x => x.Status) : items.OrderBy(x => x.Status),
			"owneruserid" => descending ? items.OrderByDescending(x => x.OwnerUserId) : items.OrderBy(x => x.OwnerUserId),
			"dueat" => descending
				? items.OrderByDescending(x => x.DueAt ?? DateTimeOffset.MinValue)
				: items.OrderBy(x => x.DueAt ?? DateTimeOffset.MaxValue),
			"agedays" => descending ? items.OrderByDescending(x => x.AgeDays) : items.OrderBy(x => x.AgeDays),
			"isurgent" => descending ? items.OrderByDescending(x => x.IsUrgent) : items.OrderBy(x => x.IsUrgent),
			"registrationstatus" => descending
				? items.OrderByDescending(x => x.Registration?.Status)
				: items.OrderBy(x => x.Registration?.Status),
			"completionpercentage" => descending
				? items.OrderByDescending(x => x.Registration?.CompletionPercentage)
				: items.OrderBy(x => x.Registration?.CompletionPercentage),
			"documentpendingcount" => descending
				? items.OrderByDescending(x => x.Registration?.DocumentPendingCount)
				: items.OrderBy(x => x.Registration?.DocumentPendingCount),
			"linkexpiresat" => descending
				? items.OrderByDescending(x => x.Registration?.LinkExpiresAt)
				: items.OrderBy(x => x.Registration?.LinkExpiresAt),
			"routetype" => descending ? items.OrderByDescending(x => x.Offer?.RouteType) : items.OrderBy(x => x.Offer?.RouteType),
			"offerstatus" => descending ? items.OrderByDescending(x => x.Offer?.OfferStatus) : items.OrderBy(x => x.Offer?.OfferStatus),
			"seatpoolname" => descending ? items.OrderByDescending(x => x.Offer?.SeatPoolName) : items.OrderBy(x => x.Offer?.SeatPoolName),
			"paymentgatesatisfied" => descending
				? items.OrderByDescending(x => x.Offer?.PaymentGateSatisfied)
				: items.OrderBy(x => x.Offer?.PaymentGateSatisfied),
			"offerexpiresat" => descending
				? items.OrderByDescending(x => x.Offer?.OfferExpiresAt)
				: items.OrderBy(x => x.Offer?.OfferExpiresAt),
			"activationstatus" => descending
				? items.OrderByDescending(x => x.Activation?.Status)
				: items.OrderBy(x => x.Activation?.Status),
			"placementconfigured" => descending
				? items.OrderByDescending(x => x.Activation?.PlacementConfigured)
				: items.OrderBy(x => x.Activation?.PlacementConfigured),
			"servicehandoffstatus" => descending
				? items.OrderByDescending(x => x.Operation?.ServiceHandoffStatus)
				: items.OrderBy(x => x.Operation?.ServiceHandoffStatus),
			_ => items.OrderByDescending(x => x.IsUrgent)
				.ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
				.ThenBy(x => x.ApplicantName)
		};
	}

	internal static int GetPriorityRank(AdmissionCrmLeadPriority priority) => priority switch
	{
		AdmissionCrmLeadPriority.Hot => 0,
		AdmissionCrmLeadPriority.Warm => 1,
		AdmissionCrmLeadPriority.Cold => 2,
		AdmissionCrmLeadPriority.Low => 3,
		_ => 4
	};

	internal static string GetUserDisplayName(IdentityUser user)
	{
		var fullName = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
	}

	internal static bool IsRegistrationOpen(AdmissionCrmRegistrationStatus status)
	{
		return status is AdmissionCrmRegistrationStatus.Issued
			or AdmissionCrmRegistrationStatus.Opened
			or AdmissionCrmRegistrationStatus.Started
			or AdmissionCrmRegistrationStatus.PartiallyCompleted
			or AdmissionCrmRegistrationStatus.ReturnedForCorrection
			or AdmissionCrmRegistrationStatus.Expired;
	}

	internal static int AgeDays(DateTime value, DateTime now) => Math.Max(0, (now.Date - value.Date).Days);

	internal static int AgeDays(DateTimeOffset value, DateTime now) => AgeDays(value.UtcDateTime, now);

	internal static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

	internal static string GetApplicantName(AdmissionInquiryLead lead)
	{
		var name = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(name) ? lead.ParentGuardianName ?? lead.PrimaryContactNo ?? "Applicant" : name;
	}

	internal static int? ParseApplicantNumber(string? value)
	{
		var normalized = value?.Trim().TrimStart('#');
		return int.TryParse(normalized, out var applicantNumber) && applicantNumber > 0
			? applicantNumber
			: null;
	}
}