using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAnalyticsPipelineCalculator
{
	private const string SlaReminderTaskTitle = "SLA follow-up required";

internal static IReadOnlyList<AdmissionCrmStageFunnelDto> BuildStageFunnel(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmPipelineStage> stages,
		int totalLeadCount)
	{
		var stageNameBySystemStage = stages
			.GroupBy(x => x.SystemStage)
			.ToDictionary(x => x.Key, x => x.OrderBy(stage => stage.DisplayOrder).First().StageName);

		return Enum.GetValues<AdmissionInquiryStage>()
			.Select(stage =>
			{
				var leadCount = leads.Count(lead => lead.Stage == stage);
				return new AdmissionCrmStageFunnelDto
				{
					Stage = stage,
					StageName = stageNameBySystemStage.GetValueOrDefault(stage, AdmissionCrmAnalyticsApplicantCalculator.ToDisplayLabel(stage.ToString())),
					LeadCount = leadCount,
					ShareOfLeads = AdmissionCrmAnalyticsApplicantCalculator.Rate(leadCount, totalLeadCount)
				};
			})
			.ToList();
	}

internal static IReadOnlyList<AdmissionCrmChannelPerformanceDto> BuildChannelPerformance(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams)
	{
		var examsByLead = exams.ToLookup(x => x.LeadId);
		return Enum.GetValues<AdmissionInquiryChannel>()
			.Select(channel =>
			{
				var channelLeads = leads.Where(x => x.Channel == channel).ToList();
				var channelExams = channelLeads.SelectMany(lead => examsByLead[lead.Id]).ToList();
				var eligibleCount = channelExams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);

				return new AdmissionCrmChannelPerformanceDto
				{
					Channel = channel,
					ChannelName = AdmissionCrmAnalyticsApplicantCalculator.ToDisplayLabel(channel.ToString()),
					LeadCount = channelLeads.Count,
					ExamScheduledCount = channelExams.Count,
					EligibleCount = eligibleCount,
					WaitlistedCount = channelExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted),
					RejectedCount = channelExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
					EligibilityRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(eligibleCount, channelLeads.Count)
				};
			})
			.Where(x => x.LeadCount > 0 || x.ExamScheduledCount > 0)
			.ToList();
	}

internal static IReadOnlyList<AdmissionCrmSourcePerformanceDto> BuildSourcePerformance(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams)
	{
		var examsByLead = exams.ToLookup(x => x.LeadId);
		return leads
			.GroupBy(x => string.IsNullOrWhiteSpace(x.InquirySourceSnapshot)
				? "Unassigned"
				: x.InquirySourceSnapshot)
			.OrderByDescending(x => x.Count())
			.Select(group =>
			{
				var sourceLeads = group.ToList();
				var sourceExams = sourceLeads.SelectMany(lead => examsByLead[lead.Id]).ToList();
				var eligibleCount = sourceExams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);

				return new AdmissionCrmSourcePerformanceDto
				{
					SourceName = group.Key,
					LeadCount = sourceLeads.Count,
					ExamScheduledCount = sourceExams.Count,
					EligibleCount = eligibleCount,
					EligibilityRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(eligibleCount, sourceLeads.Count)
				};
			})

			.ToList();
	}

internal static IReadOnlyList<AdmissionCrmCampaignPerformanceDto> BuildCampaignPerformance(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams)
	{
		var examsByLead = exams.ToLookup(x => x.LeadId);
		return leads
			.Where(x => !string.IsNullOrWhiteSpace(x.CampaignName))
			.GroupBy(x => x.CampaignName!)
			.OrderByDescending(x => x.Count())
			.ThenBy(x => x.Key)
			.Select(group =>
			{
				var campaignLeads = group.ToList();
				var campaignExams = campaignLeads.SelectMany(lead => examsByLead[lead.Id]).ToList();
				var eligibleCount = campaignExams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);

				return new AdmissionCrmCampaignPerformanceDto
				{
					CampaignName = group.Key,
					LeadCount = campaignLeads.Count,
					ExamScheduledCount = campaignExams.Count,
					EligibleCount = eligibleCount,
					WaitlistedCount = campaignExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted),
					RejectedCount = campaignExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
					EligibilityRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(eligibleCount, campaignLeads.Count),
					AverageLeadScore = Math.Round((decimal)campaignLeads.Average(x => x.LeadScore), 2)
				};
			})
			.ToList();
	}

internal static AdmissionCrmTaskAnalyticsDto BuildTaskAnalytics(
		IReadOnlyCollection<AdmissionCrmFollowUpTask> tasks,
		DateTime now)
	{
		return new AdmissionCrmTaskAnalyticsDto
		{
			PendingCount = tasks.Count(x => x.Status == AdmissionCrmTaskStatus.Pending),
			CompletedCount = tasks.Count(x => x.Status == AdmissionCrmTaskStatus.Completed),
			OverdueCount = tasks.Count(x =>
				x.Status == AdmissionCrmTaskStatus.Pending &&
				x.DueAt < now)
		};
	}

internal static AdmissionCrmSlaAnalyticsDto BuildSlaAnalytics(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmTimelineEvent> timelineEvents,
		IReadOnlyCollection<AdmissionCrmFollowUpTask> tasks,
		int firstResponseSlaHours,
		DateTimeOffset now)
	{
		var firstResponseEventsByLead = timelineEvents
			.Where(IsFirstResponseEvent)
			.GroupBy(x => x.LeadId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderBy(timelineEvent => timelineEvent.OccurredAt)
					.ThenBy(timelineEvent => timelineEvent.Id)
					.First());
		var responseHours = new List<double>();
		var firstResponseCount = 0;
		var firstResponsePendingCount = 0;
		var firstResponseSlaMetCount = 0;
		var firstResponseSlaMissedCount = 0;

		foreach (var lead in leads)
		{
			var createdAt = new DateTimeOffset(DateTime.SpecifyKind(lead.CreationTime, DateTimeKind.Utc));
			var dueAt = createdAt.AddHours(firstResponseSlaHours);
			if (firstResponseEventsByLead.TryGetValue(lead.Id, out var firstResponseEvent))
			{
				firstResponseCount++;
				var hoursToRespond = Math.Max(
					0,
					(firstResponseEvent.OccurredAt - createdAt).TotalHours);
				responseHours.Add(hoursToRespond);

				if (firstResponseEvent.OccurredAt <= dueAt)
				{
					firstResponseSlaMetCount++;
				}
				else
				{
					firstResponseSlaMissedCount++;
				}

				continue;
			}

			if (now > dueAt)
			{
				firstResponseSlaMissedCount++;
			}
			else
			{
				firstResponsePendingCount++;
			}
		}

		var followUpSlaMissedCount = tasks.Count(x => x.Title == SlaReminderTaskTitle);
		return new AdmissionCrmSlaAnalyticsDto
		{
			FirstResponseSlaHours = firstResponseSlaHours,
			FirstResponseCount = firstResponseCount,
			FirstResponsePendingCount = firstResponsePendingCount,
			FirstResponseSlaMetCount = firstResponseSlaMetCount,
			FirstResponseSlaMissedCount = firstResponseSlaMissedCount,
			FirstResponseRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(firstResponseCount, leads.Count),
			FirstResponseSlaMetRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(firstResponseSlaMetCount, firstResponseCount),
			FirstResponseSlaMissedRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(firstResponseSlaMissedCount, leads.Count),
			AverageFirstResponseHours = responseHours.Count == 0
				? 0
				: Math.Round((decimal)responseHours.Average(), 2),
			FollowUpSlaMissedCount = followUpSlaMissedCount,
			FollowUpSlaMissedRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(followUpSlaMissedCount, leads.Count)
		};
	}

internal static bool IsFirstResponseEvent(AdmissionCrmTimelineEvent timelineEvent)
	{
		if (!timelineEvent.ActorUserId.HasValue)
		{
			return false;
		}

		return timelineEvent.EventType is
			AdmissionCrmTimelineEventType.NoteAdded or
			AdmissionCrmTimelineEventType.TaskCompleted or
			AdmissionCrmTimelineEventType.StageChanged or
			AdmissionCrmTimelineEventType.MessageTemplateChanged or
			AdmissionCrmTimelineEventType.RegistrationFormIssued or
			AdmissionCrmTimelineEventType.ExamScheduled or
			AdmissionCrmTimelineEventType.ExamAttendanceMarked or
			AdmissionCrmTimelineEventType.ExamResultRecorded or
			AdmissionCrmTimelineEventType.EligibilityDecided or
			AdmissionCrmTimelineEventType.Archived or
			AdmissionCrmTimelineEventType.Reopened or
			AdmissionCrmTimelineEventType.DuplicateMerged;
	}

internal static IReadOnlyList<AdmissionCrmStaffWorkloadDto> BuildStaffWorkload(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmFollowUpTask> tasks,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		DateTime now)
	{
		var examsByLead = exams.ToLookup(x => x.LeadId);
		var ownerIds = leads
			.Select(x => x.OwnerUserId)
			.Concat(tasks.Select(x => x.AssignedUserId))
			.Distinct()
			.ToList();

		return ownerIds
			.Select(ownerUserId =>
			{
				var ownerLeads = leads.Where(x => x.OwnerUserId == ownerUserId).ToList();
				var ownerTasks = tasks.Where(x => x.AssignedUserId == ownerUserId).ToList();

				return new AdmissionCrmStaffWorkloadDto
				{
					OwnerUserId = ownerUserId,
					OwnerLabel = ownerUserId?.ToString("D") ?? "Unassigned",
					LeadCount = ownerLeads.Count,
					OpenLeadCount = ownerLeads.Count(x =>
						x.Stage is not AdmissionInquiryStage.Archived
							and not AdmissionInquiryStage.Eligible
							and not AdmissionInquiryStage.Rejected),
					StaleLeadCount = ownerLeads.Count(x => x.Stage == AdmissionInquiryStage.Stale),
					PendingTaskCount = ownerTasks.Count(x => x.Status == AdmissionCrmTaskStatus.Pending),
					OverdueTaskCount = ownerTasks.Count(x =>
						x.Status == AdmissionCrmTaskStatus.Pending &&
						x.DueAt < now),
					ExamScheduledCount = ownerLeads.Sum(lead => examsByLead[lead.Id].Count())
				};
			})
			.Where(x =>
				x.LeadCount > 0 ||
				x.PendingTaskCount > 0 ||
				x.OverdueTaskCount > 0 ||
				x.ExamScheduledCount > 0)
			.OrderByDescending(x => x.OpenLeadCount)
			.ThenByDescending(x => x.PendingTaskCount)
			.ThenBy(x => x.OwnerLabel)
			.ToList();
	}
}

