using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmLeadMapper
{
	internal static AdmissionCrmSlaHoldDto MapSlaHold(AdmissionCrmSlaHold hold) => new()
	{
		Id = hold.Id,
		AcademicYearId = hold.AcademicYearId,
		LeadId = hold.LeadId,
		HoldReason = hold.HoldReason,
		Reason = hold.Reason,
		StartedAt = hold.StartedAt,
		ResumedAt = hold.ResumedAt,
		ResumeReason = hold.ResumeReason,
		IsActive = hold.IsActive
	};

	internal static AdmissionCrmLeadDto ToDto(
		AdmissionInquiryLead lead,
		IReadOnlyList<AdmissionCrmLeadDuplicateDto> duplicates,
		AdmissionCrmApplicantProgressDto? progress = null)
	{
		return new AdmissionCrmLeadDto
		{
			Id = lead.Id,
			ApplicantNumber = lead.ApplicantNumber,
			AcademicYearId = lead.AcademicYearId,
			AdmissionYearSetupId = lead.AdmissionYearSetupId,
			AdmissionYearSetupVersion = lead.AdmissionYearSetupVersion,
			StudentId = lead.StudentId,
			InquirySourceId = lead.InquirySourceId,
			InquirySourceSnapshot = lead.InquirySourceSnapshot,
			InquiryTypeId = lead.InquiryTypeId,
			InquiryTypeSnapshot = lead.InquiryTypeSnapshot,
			Channel = lead.Channel,
			CampaignName = lead.CampaignName,
			CampusBranchName = lead.CampusBranchName,
			PreferredCommunicationLanguage = lead.PreferredCommunicationLanguage,
			FirstName = lead.FirstName,
			MiddleName = lead.MiddleName,
			LastName = lead.LastName,
			DateOfBirth = lead.DateOfBirth,
			ParentGuardianName = lead.ParentGuardianName,
			PrimaryContactNo = lead.PrimaryContactNo,
			Email = lead.Email,
			AdmissionIntent = lead.AdmissionIntent,
			IntendedRouteType = lead.IntendedRouteType,
			IntendedRouteDefinitionId = lead.IntendedRouteDefinitionId,
			IntendedRouteDefinitionVersion = lead.IntendedRouteDefinitionVersion,
			IntendedRouteNameSnapshot = lead.IntendedRouteNameSnapshot,
			TargetStandardId = lead.TargetStandardId,
			DesiredClassName = lead.DesiredClassName,
			Stage = lead.Stage,
			OwnerUserId = lead.OwnerUserId,
			LeadScore = lead.LeadScore,
			Priority = lead.Priority,
			RegistrationFormNumber = lead.RegistrationFormNumber,
			RegistrationFormAmount = lead.RegistrationFormAmount,
			RegistrationFormIssuedAt = lead.RegistrationFormIssuedAt,
			LastActivityTime = lead.LastActivityTime,
			Notes = lead.Notes,
			ArchivedAt = lead.ArchivedAt,
			ArchiveReason = lead.ArchiveReason,
			ReopenedAt = lead.ReopenedAt,
			ReopenReason = lead.ReopenReason,
			MergedIntoLeadId = lead.MergedIntoLeadId,
			MergedAt = lead.MergedAt,
			MergeReason = lead.MergeReason,
			DuplicateContinueReason = lead.DuplicateContinueReason,
			PipelineColumn = progress?.PipelineColumn,
			ClosedOutcome = progress?.ClosedOutcome,
			CurrentStep = progress?.CurrentStep,
			NextAction = progress?.NextAction,
			NextActionCode = progress?.NextActionCode,
			LikelyDuplicateCount = duplicates.Count,
			LikelyDuplicates = duplicates
		};
	}

	internal static AdmissionCrmFollowUpTaskDto ToDto(AdmissionCrmFollowUpTask task)
	{
		return new AdmissionCrmFollowUpTaskDto
		{
			Id = task.Id,
			LeadId = task.LeadId,
			AcademicYearId = task.AcademicYearId,
			Title = task.Title,
			DueAt = task.DueAt,
			AssignedUserId = task.AssignedUserId,
			Status = task.Status,
			Notes = task.Notes,
			CompletedAt = task.CompletedAt,
			CompletionNotes = task.CompletionNotes
		};
	}

	internal static AdmissionCrmTimelineEventDto ToDto(AdmissionCrmTimelineEvent timelineEvent)
	{
		return new AdmissionCrmTimelineEventDto
		{
			Id = timelineEvent.Id,
			LeadId = timelineEvent.LeadId,
			AcademicYearId = timelineEvent.AcademicYearId,
			EventType = timelineEvent.EventType,
			Title = timelineEvent.Title,
			Details = timelineEvent.Details,
			RelatedEntityId = timelineEvent.RelatedEntityId,
			ActorUserId = timelineEvent.ActorUserId,
			OccurredAt = timelineEvent.OccurredAt
		};
	}

	internal static AdmissionCrmApplicantChecklistProgressDto ToApplicantChecklistProgressDto(
		AdmissionCrmApplicantChecklistItemDto item,
		AdmissionCrmApplicantChecklistProgress? progress,
		AdmissionInquiryLead lead)
	{
		var status = progress?.Status ?? AdmissionCrmApplicantChecklistProgressStatus.Pending;
		return new AdmissionCrmApplicantChecklistProgressDto
		{
			Id = progress?.Id,
			LeadId = lead.Id,
			AcademicYearId = lead.AcademicYearId,
			Key = item.Key,
			Label = item.Label,
			Details = item.Details,
			IsRequired = item.IsRequired,
			Status = status,
			StatusLabel = GetApplicantChecklistProgressStatusLabel(status),
			IsComplete = IsApplicantChecklistComplete(status),
			Notes = progress?.Notes,
			StatusChangedAt = progress?.StatusChangedAt
		};
	}

	internal static string BuildApplicantChecklistTimelineDetails(
		AdmissionCrmApplicantChecklistItemDto item,
		AdmissionCrmApplicantChecklistProgress progress)
	{
		var label = GetApplicantChecklistProgressStatusLabel(progress.Status);
		return string.IsNullOrWhiteSpace(progress.Notes)
			? $"{item.Label}: {label}"
			: $"{item.Label}: {label}. {progress.Notes}";
	}

	internal static string GetApplicantChecklistProgressStatusLabel(
		AdmissionCrmApplicantChecklistProgressStatus status)
	{
		return status switch
		{
			AdmissionCrmApplicantChecklistProgressStatus.Requested => "Requested",
			AdmissionCrmApplicantChecklistProgressStatus.Received => "Received",
			AdmissionCrmApplicantChecklistProgressStatus.Waived => "Waived",
			_ => "Pending"
		};
	}

	internal static bool IsApplicantChecklistComplete(AdmissionCrmApplicantChecklistProgressStatus status)
	{
		return status is AdmissionCrmApplicantChecklistProgressStatus.Received or
			AdmissionCrmApplicantChecklistProgressStatus.Waived;
	}
}

internal static class AdmissionCrmLeadFormat
{
	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}