using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsSummaryWorkflow(
	AdmissionCrmAnalyticsCoreReader coreReader,
	AdmissionCrmAnalyticsApplicantReader applicantReader,
	AdmissionCrmAnalyticsDecisionReader decisionReader,
	AdmissionCrmAnalyticsIdentityReader identityReader,
	AdmissionCrmAnalyticsGovernanceReader governanceReader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmAnalyticsSummaryDto> GetAsync(Guid yearId, GetAdmissionCrmAnalyticsSummaryInput? input)
	{
		input ??= new GetAdmissionCrmAnalyticsSummaryInput();
		AdmissionCrmAnalyticsFilters.ValidateAnalyticsInput(input);
		var (trendFrom, trendTo) = AdmissionCrmAnalyticsFilters.GetTrendDateRange(input, Clock.Now.Date);
		var core = await coreReader.LoadAsync(yearId, input);
		var leadIds = core.Leads.Select(x => x.Id).ToList();
		var applicant = await applicantReader.LoadAsync(yearId, leadIds, input);
		var decision = await decisionReader.LoadAsync(yearId, leadIds);
		var identity = await identityReader.LoadAsync(yearId, leadIds);
		var governance = await governanceReader.LoadAsync(yearId, leadIds);
		var failures = core.SummaryTimelineEvents.Where(x =>
			x.EventType == AdmissionCrmTimelineEventType.AutomationFailed).ToList();
		var recentFailures = failures.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(5).ToList();
		var firstResponseHours = core.Stages.FirstOrDefault(x => x.SystemStage == AdmissionInquiryStage.NewInquiry)
			?.SlaTargetHours ?? core.CycleConfiguration?.DefaultLeadSlaHours ?? 24;
		var total = core.Leads.Count;
		var duplicates = AdmissionCrmAnalyticsDuplicateDetector.CountLikelyDuplicateLeads(core.Leads);
		var scheduled = core.Exams.Select(x => x.LeadId).Distinct().Count();
		var handedOff = core.Exams.Where(x => x.EligibilityDecision is not AdmissionCrmEligibilityDecision.Pending)
			.Select(x => x.LeadId).Distinct().Count();
		var stale = core.Leads.Count(x => x.Stage == AdmissionInquiryStage.Stale);
		var archived = core.Leads.Count(x => x.Stage == AdmissionInquiryStage.Archived);
		var staleOrArchived = stale + archived;
		return new AdmissionCrmAnalyticsSummaryDto
		{
			AcademicYearId = yearId, TrendFromDate = trendFrom, TrendToDate = trendTo, TotalLeadCount = total,
			ActiveLeadCount = core.Leads.Count(x => x.Stage is not AdmissionInquiryStage.Archived and not AdmissionInquiryStage.Rejected),
			ExamScheduledLeadCount = scheduled, EligibilityHandoffCount = handedOff, StaleLeadCount = stale,
			ArchivedLeadCount = archived, StaleOrArchivedLeadCount = staleOrArchived, DuplicateWarningCount = duplicates,
			ExamScheduledRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(scheduled, total),
			EligibilityHandoffRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(handedOff, total),
			StaleRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(stale, total),
			ArchiveRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(archived, total),
			StaleOrArchivedRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(staleOrArchived, total),
			DuplicateWarningRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(duplicates, total),
			AverageLeadScore = total == 0 ? 0 : Math.Round((decimal)core.Leads.Average(x => x.LeadScore), 2),
			StageFunnel = AdmissionCrmAnalyticsPipelineCalculator.BuildStageFunnel(core.Leads, core.Stages, total),
			ChannelPerformance = AdmissionCrmAnalyticsPipelineCalculator.BuildChannelPerformance(core.Leads, core.Exams),
			SourcePerformance = AdmissionCrmAnalyticsPipelineCalculator.BuildSourcePerformance(core.Leads, core.Exams),
			CampaignPerformance = AdmissionCrmAnalyticsPipelineCalculator.BuildCampaignPerformance(core.Leads, core.Exams),
			StaffWorkload = AdmissionCrmAnalyticsPipelineCalculator.BuildStaffWorkload(core.Leads, core.Tasks, core.Exams, Clock.Now),
			TaskAnalytics = AdmissionCrmAnalyticsPipelineCalculator.BuildTaskAnalytics(core.Tasks, Clock.Now),
			SlaAnalytics = AdmissionCrmAnalyticsPipelineCalculator.BuildSlaAnalytics(core.Leads,
				core.DimensionTimelineEvents.Where(x => leadIds.Contains(x.LeadId)).ToList(), core.Tasks,
				firstResponseHours, new DateTimeOffset(Clock.Now)),
			ExamAnalytics = AdmissionCrmAnalyticsApplicantCalculator.BuildExamAnalytics(core.Exams, core.Leads, applicant.Slots),
			RegistrationAnalytics = AdmissionCrmAnalyticsApplicantCalculator.BuildRegistrationAnalytics(applicant.Registrations, total, Clock.Now),
			DocumentAnalytics = AdmissionCrmAnalyticsApplicantCalculator.BuildDocumentAnalytics(applicant.Checklists, applicant.Documents, Clock.Now),
			FinalAdmissionAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildFinalAdmissionAnalytics(decision.SeatPools,
				decision.SeatOffers, decision.SeatReservations, decision.WaitlistEntries, decision.AdmissionDecisions),
			FinancialAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildFinancialAnalytics(decision.Quotations, decision.Concessions, Clock.Now),
			HouseholdIdentityAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildHouseholdIdentityAnalytics(
				identity.IdentityProfiles, identity.GuardianRelationships, identity.ApplicantPeople),
			PolicyAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildPolicyAnalytics(governance.PolicyPacks,
				governance.PolicyAdoptions, governance.PolicyEvaluations, governance.PolicyExceptions, Clock.Now),
			RouteAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildRouteAnalytics(governance.RouteAssignments, governance.RouteAppeals),
			ActivationAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildActivationAnalytics(governance.Activations, governance.Identifiers),
			AutomationAnalytics = AdmissionCrmAnalyticsGovernanceCalculator.BuildAutomationAnalytics(core.AutomationRuns, failures.Count, recentFailures),
			DailyTrends = AdmissionCrmAnalyticsFilters.BuildDailyTrends(core.DimensionLeads, core.DimensionExams,
				core.DimensionTasks, core.DimensionTimelineEvents, trendFrom, trendTo)
		};
	}
}
