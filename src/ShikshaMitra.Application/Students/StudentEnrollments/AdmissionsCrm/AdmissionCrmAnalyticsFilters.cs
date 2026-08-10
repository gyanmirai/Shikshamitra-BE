using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAnalyticsFilters
{
	internal const int DefaultTrendWindowDays = 30;
	internal const int MaxTrendWindowDays = 366;
	private const string SlaReminderTaskTitle = "SLA follow-up required";

internal static IEnumerable<AdmissionInquiryLead> ApplyDimensionFilters(
		IEnumerable<AdmissionInquiryLead> leads,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var query = leads;

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		if (input.InquirySourceId.HasValue)
		{
			query = query.Where(x => x.InquirySourceId == input.InquirySourceId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.CampaignName))
		{
			var campaignName = input.CampaignName.Trim();
			query = query.Where(x => string.Equals(
				x.CampaignName,
				campaignName,
				StringComparison.OrdinalIgnoreCase));
		}

		if (input.OwnerUserId.HasValue)
		{
			query = query.Where(x => x.OwnerUserId == input.OwnerUserId.Value);
		}

		if (input.Stage.HasValue)
		{
			query = query.Where(x => x.Stage == input.Stage.Value);
		}

		if (input.TargetStandardId.HasValue)
		{
			query = query.Where(x => x.TargetStandardId == input.TargetStandardId.Value);
		}

		if (input.MinimumLeadScore.HasValue)
		{
			query = query.Where(x => x.LeadScore >= input.MinimumLeadScore.Value);
		}

		if (input.MaximumLeadScore.HasValue)
		{
			query = query.Where(x => x.LeadScore <= input.MaximumLeadScore.Value);
		}

		return query;
	}

internal static IEnumerable<AdmissionInquiryLead> ApplyLeadDateFilter(
		IEnumerable<AdmissionInquiryLead> leads,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return leads.Where(x => IsInOptionalDateRange(x.CreationTime, input));
	}

internal static IEnumerable<AdmissionCrmEntranceExamRecord> ApplyExamDateFilter(
		IEnumerable<AdmissionCrmEntranceExamRecord> exams,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return exams.Where(x => IsInOptionalDateRange(x.ScheduledAt, input));
	}

internal static IEnumerable<AdmissionCrmFollowUpTask> ApplyTaskDateFilter(
		IEnumerable<AdmissionCrmFollowUpTask> tasks,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return tasks.Where(x => IsInOptionalDateRange(x.DueAt, input));
	}

internal static IEnumerable<AdmissionCrmTimelineEvent> ApplyTimelineDateFilter(
		IEnumerable<AdmissionCrmTimelineEvent> events,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return events.Where(x => IsInOptionalDateRange(x.OccurredAt, input));
	}

internal static IEnumerable<AdmissionCrmAutomationRun> ApplyAutomationRunDateFilter(
		IEnumerable<AdmissionCrmAutomationRun> runs,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return runs.Where(x => IsInOptionalDateRange(x.CompletedAt, input));
	}

internal static bool IsInOptionalDateRange(DateTime value, GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var date = value.Date;
		return (!input.FromDate.HasValue || date >= input.FromDate.Value.Date) &&
		       (!input.ToDate.HasValue || date <= input.ToDate.Value.Date);
	}

internal static bool IsInOptionalDateRange(DateTimeOffset value, GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var date = value.UtcDateTime.Date;
		return (!input.FromDate.HasValue || date >= input.FromDate.Value.Date) &&
		       (!input.ToDate.HasValue || date <= input.ToDate.Value.Date);
	}

internal static (DateTime FromDate, DateTime ToDate) GetTrendDateRange(
		GetAdmissionCrmAnalyticsSummaryInput input,
		DateTime today)
	{
		var toDate = (input.ToDate ?? today).Date;
		var fromDate = (input.FromDate ?? toDate.AddDays(-(DefaultTrendWindowDays - 1))).Date;

		if (fromDate > toDate)
		{
			throw new BusinessException("AdmissionCrm:AnalyticsInvalidDateRange")
				.WithData("FromDate", fromDate)
				.WithData("ToDate", toDate);
		}

		if ((toDate - fromDate).TotalDays >= MaxTrendWindowDays)
		{
			throw new BusinessException("AdmissionCrm:AnalyticsDateRangeTooLarge")
				.WithData("MaxDays", MaxTrendWindowDays);
		}

		return (fromDate, toDate);
	}

internal static void ValidateAnalyticsInput(GetAdmissionCrmAnalyticsSummaryInput input)
	{
		if (input.MinimumLeadScore is < 0 or > 100)
		{
			throw new BusinessException("AdmissionCrm:InvalidLeadScoreFilter")
				.WithData("MinimumLeadScore", input.MinimumLeadScore);
		}

		if (input.MaximumLeadScore is < 0 or > 100)
		{
			throw new BusinessException("AdmissionCrm:InvalidLeadScoreFilter")
				.WithData("MaximumLeadScore", input.MaximumLeadScore);
		}

		if (input.MinimumLeadScore.HasValue &&
		    input.MaximumLeadScore.HasValue &&
		    input.MinimumLeadScore.Value > input.MaximumLeadScore.Value)
		{
			throw new BusinessException("AdmissionCrm:InvalidLeadScoreFilter")
				.WithData("MinimumLeadScore", input.MinimumLeadScore)
				.WithData("MaximumLeadScore", input.MaximumLeadScore);
		}
	}

internal static IReadOnlyList<AdmissionCrmDailyTrendDto> BuildDailyTrends(
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionCrmFollowUpTask> tasks,
		IReadOnlyCollection<AdmissionCrmTimelineEvent> timelineEvents,
		DateTime fromDate,
		DateTime toDate)
	{
		var trends = new List<AdmissionCrmDailyTrendDto>();
		for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
		{
			trends.Add(new AdmissionCrmDailyTrendDto
			{
				Date = date,
				LeadCount = leads.Count(x => IsSameDate(x.CreationTime, date)),
				ExamScheduledCount = exams.Count(x => IsSameDate(x.ScheduledAt, date)),
				ExamCompletedCount = exams.Count(x =>
					x.ResultRecordedAt.HasValue &&
					IsSameDate(x.ResultRecordedAt.Value, date)),
				EligibleCount = exams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible &&
					x.EligibilityDecidedAt.HasValue &&
					IsSameDate(x.EligibilityDecidedAt.Value, date)),
				WaitlistedCount = exams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted &&
					x.EligibilityDecidedAt.HasValue &&
					IsSameDate(x.EligibilityDecidedAt.Value, date)),
				RejectedCount = exams.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected &&
					x.EligibilityDecidedAt.HasValue &&
					IsSameDate(x.EligibilityDecidedAt.Value, date)),
				StaleCount = timelineEvents.Count(x =>
					x.EventType == AdmissionCrmTimelineEventType.MarkedStale &&
					IsSameDate(x.OccurredAt, date)),
				ArchivedCount = timelineEvents.Count(x =>
					(x.EventType is AdmissionCrmTimelineEventType.Archived
						or AdmissionCrmTimelineEventType.DuplicateMerged) &&
					IsSameDate(x.OccurredAt, date)),
				SlaMissedCount = tasks.Count(x =>
					x.Title == SlaReminderTaskTitle &&
					IsSameDate(x.CreationTime, date))
			});
		}

		return trends;
	}

internal static bool IsSameDate(DateTime value, DateTime date)
	{
		return value.Date == date.Date;
	}

internal static bool IsSameDate(DateTimeOffset value, DateTime date)
	{
		return value.UtcDateTime.Date == date.Date;
	}
}
