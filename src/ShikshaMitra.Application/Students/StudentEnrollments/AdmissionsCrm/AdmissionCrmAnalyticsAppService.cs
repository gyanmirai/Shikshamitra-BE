using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.Compliance;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmAnalyticsAppService : ApplicationService, IAdmissionCrmAnalyticsAppService
{
	private const string SlaReminderTaskTitle = "SLA follow-up required";
	private const int DefaultTrendWindowDays = 30;
	private const int MaxTrendWindowDays = 366;

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmFollowUpTask, Guid> _taskRepository;
	private readonly IRepository<AdmissionCrmPipelineStage, Guid> _pipelineStageRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineEventRepository;
	private readonly IRepository<AdmissionCrmAutomationRun, Guid> _automationRunRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleConfigurationRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _checklistRepository;
	private readonly IRepository<AdmissionCrmApplicantDocument, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _seatOfferRepository;
	private readonly IRepository<AdmissionCrmSeatReservation, Guid> _seatReservationRepository;
	private readonly IRepository<AdmissionCrmWaitlistEntry, Guid> _waitlistRepository;
	private readonly IRepository<AdmissionCrmAdmissionDecision, Guid> _admissionDecisionRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmConcessionRequest, Guid> _concessionRepository;
	private readonly IRepository<AdmissionCrmApplicantIdentityProfile, Guid> _identityProfileRepository;
	private readonly IRepository<AdmissionCrmGuardianRelationship, Guid> _guardianRelationshipRepository;
	private readonly IRepository<AdmissionCrmPerson, Guid> _personRepository;
	private readonly IRepository<AdmissionCrmPolicyPack, Guid> _policyPackRepository;
	private readonly IRepository<AdmissionCrmPolicyAdoption, Guid> _policyAdoptionRepository;
	private readonly IRepository<AdmissionCrmPolicyEvaluation, Guid> _policyEvaluationRepository;
	private readonly IRepository<AdmissionCrmPolicyException, Guid> _policyExceptionRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _routeAssignmentRepository;
	private readonly IRepository<AdmissionCrmRouteAppeal, Guid> _routeAppealRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmIdentifierAllocation, Guid> _identifierAllocationRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmAnalyticsAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
		IRepository<AdmissionCrmPipelineStage, Guid> pipelineStageRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineEventRepository,
		IRepository<AdmissionCrmAutomationRun, Guid> automationRunRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleConfigurationRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
		IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> seatOfferRepository,
		IRepository<AdmissionCrmSeatReservation, Guid> seatReservationRepository,
		IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
		IRepository<AdmissionCrmAdmissionDecision, Guid> admissionDecisionRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmConcessionRequest, Guid> concessionRepository,
		IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityProfileRepository,
		IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRelationshipRepository,
		IRepository<AdmissionCrmPerson, Guid> personRepository,
		IRepository<AdmissionCrmPolicyPack, Guid> policyPackRepository,
		IRepository<AdmissionCrmPolicyAdoption, Guid> policyAdoptionRepository,
		IRepository<AdmissionCrmPolicyEvaluation, Guid> policyEvaluationRepository,
		IRepository<AdmissionCrmPolicyException, Guid> policyExceptionRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> routeAssignmentRepository,
		IRepository<AdmissionCrmRouteAppeal, Guid> routeAppealRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierAllocationRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_examRepository = examRepository;
		_taskRepository = taskRepository;
		_pipelineStageRepository = pipelineStageRepository;
		_timelineEventRepository = timelineEventRepository;
		_automationRunRepository = automationRunRepository;
		_cycleConfigurationRepository = cycleConfigurationRepository;
		_registrationRepository = registrationRepository;
		_checklistRepository = checklistRepository;
		_documentRepository = documentRepository;
		_slotRepository = slotRepository;
		_seatPoolRepository = seatPoolRepository;
		_seatOfferRepository = seatOfferRepository;
		_seatReservationRepository = seatReservationRepository;
		_waitlistRepository = waitlistRepository;
		_admissionDecisionRepository = admissionDecisionRepository;
		_quotationRepository = quotationRepository;
		_concessionRepository = concessionRepository;
		_identityProfileRepository = identityProfileRepository;
		_guardianRelationshipRepository = guardianRelationshipRepository;
		_personRepository = personRepository;
		_policyPackRepository = policyPackRepository;
		_policyAdoptionRepository = policyAdoptionRepository;
		_policyEvaluationRepository = policyEvaluationRepository;
		_policyExceptionRepository = policyExceptionRepository;
		_routeAssignmentRepository = routeAssignmentRepository;
		_routeAppealRepository = routeAppealRepository;
		_activationRepository = activationRepository;
		_identifierAllocationRepository = identifierAllocationRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public async Task<AdmissionCrmAnalyticsSummaryDto> GetSummaryAsync(
		Guid academicYearId,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		input ??= new GetAdmissionCrmAnalyticsSummaryInput();
		ValidateAnalyticsInput(input);
		var (trendFromDate, trendToDate) = GetTrendDateRange(input, Clock.Now.Date);

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetSummaryAsync));
		var hasAcademicYearWideScope =
			await _studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);

		var leadsQueryable = await _leadRepository.GetQueryableAsync();
		var leadQuery = leadsQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);
		leadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(leadQuery);
		var allLeads = await AsyncExecuter.ToListAsync(leadQuery);
		var dimensionLeads = ApplyDimensionFilters(allLeads, input).ToList();
		var dimensionLeadIds = dimensionLeads.Select(x => x.Id).ToHashSet();
		var leads = ApplyLeadDateFilter(dimensionLeads, input).ToList();
		var summaryLeadIds = leads.Select(x => x.Id).ToHashSet();

		var examsQueryable = await _examRepository.GetQueryableAsync();
		var allExams = await AsyncExecuter.ToListAsync(examsQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		var dimensionExams = allExams
			.Where(x => dimensionLeadIds.Contains(x.LeadId))
			.ToList();
		var exams = ApplyExamDateFilter(
				dimensionExams.Where(x => summaryLeadIds.Contains(x.LeadId)),
				input)
			.ToList();

		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		var registrations = (await AsyncExecuter.ToListAsync(registrationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId) && IsInOptionalDateRange(x.IssuedAt, input))
			.ToList();
		var checklistQueryable = await _checklistRepository.GetQueryableAsync();
		var checklists = (await AsyncExecuter.ToListAsync(checklistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var documentQueryable = await _documentRepository.GetQueryableAsync();
		var documents = (await AsyncExecuter.ToListAsync(documentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var slotQueryable = await _slotRepository.GetQueryableAsync();
		var slots = await AsyncExecuter.ToListAsync(slotQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var seatPoolQueryable = await _seatPoolRepository.GetQueryableAsync();
		var seatPools = await AsyncExecuter.ToListAsync(seatPoolQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var seatOfferQueryable = await _seatOfferRepository.GetQueryableAsync();
		var seatOffers = (await AsyncExecuter.ToListAsync(seatOfferQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var seatReservationQueryable = await _seatReservationRepository.GetQueryableAsync();
		var seatReservations = (await AsyncExecuter.ToListAsync(seatReservationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var waitlistQueryable = await _waitlistRepository.GetQueryableAsync();
		var waitlistEntries = (await AsyncExecuter.ToListAsync(waitlistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var admissionDecisionQueryable = await _admissionDecisionRepository.GetQueryableAsync();
		var admissionDecisions = (await AsyncExecuter.ToListAsync(admissionDecisionQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var quotationQueryable = await _quotationRepository.GetQueryableAsync();
		var quotations = (await AsyncExecuter.ToListAsync(quotationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var concessionQueryable = await _concessionRepository.GetQueryableAsync();
		var concessions = (await AsyncExecuter.ToListAsync(concessionQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var identityProfileQueryable = await _identityProfileRepository.GetQueryableAsync();
		var identityProfiles = (await AsyncExecuter.ToListAsync(identityProfileQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => summaryLeadIds.Contains(x.LeadId))
			.ToList();
		var profileLeadIds = identityProfiles.Select(x => x.LeadId).ToHashSet();
		var guardianRelationshipQueryable = await _guardianRelationshipRepository.GetQueryableAsync();
		var guardianRelationships = (await AsyncExecuter.ToListAsync(guardianRelationshipQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)))
			.Where(x => profileLeadIds.Contains(x.LeadId) && x.IsActive)
			.ToList();
		var applicantPersonIds = identityProfiles.Select(x => x.ApplicantPersonId).ToHashSet();
		var personQueryable = await _personRepository.GetQueryableAsync();
		var applicantPeople = (await AsyncExecuter.ToListAsync(personQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id)))
			.Where(x => applicantPersonIds.Contains(x.Id))
			.ToList();
		var policyPackQueryable = await _policyPackRepository.GetQueryableAsync();
		var policyPacks = await AsyncExecuter.ToListAsync(policyPackQueryable.Where(x => x.TenantId == CurrentTenant.Id));
		var policyAdoptionQueryable = await _policyAdoptionRepository.GetQueryableAsync();
		var policyAdoptions = await AsyncExecuter.ToListAsync(policyAdoptionQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var policyEvaluationQueryable = await _policyEvaluationRepository.GetQueryableAsync();
		var policyEvaluations = (await AsyncExecuter.ToListAsync(policyEvaluationQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();
		var policyExceptionQueryable = await _policyExceptionRepository.GetQueryableAsync();
		var policyExceptions = (await AsyncExecuter.ToListAsync(policyExceptionQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();
		var routeAssignmentQueryable = await _routeAssignmentRepository.GetQueryableAsync();
		var routeAssignments = (await AsyncExecuter.ToListAsync(routeAssignmentQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();
		var routeAppealQueryable = await _routeAppealRepository.GetQueryableAsync();
		var routeAppeals = (await AsyncExecuter.ToListAsync(routeAppealQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();
		var activationQueryable = await _activationRepository.GetQueryableAsync();
		var activations = (await AsyncExecuter.ToListAsync(activationQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();
		var identifierQueryable = await _identifierAllocationRepository.GetQueryableAsync();
		var identifiers = (await AsyncExecuter.ToListAsync(identifierQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId))).Where(x => summaryLeadIds.Contains(x.LeadId)).ToList();

		var tasksQueryable = await _taskRepository.GetQueryableAsync();
		var allTasks = await AsyncExecuter.ToListAsync(tasksQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		var dimensionTasks = allTasks
			.Where(x => dimensionLeadIds.Contains(x.LeadId))
			.ToList();
		var tasks = ApplyTaskDateFilter(
				dimensionTasks.Where(x => summaryLeadIds.Contains(x.LeadId)),
				input)
			.ToList();

		var stageQueryable = await _pipelineStageRepository.GetQueryableAsync();
		var stages = await AsyncExecuter.ToListAsync(stageQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));

		var timelineQueryable = await _timelineEventRepository.GetQueryableAsync();
		var allTimelineEvents = await AsyncExecuter.ToListAsync(timelineQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		var dimensionTimelineEvents = allTimelineEvents
			.Where(x => dimensionLeadIds.Contains(x.LeadId))
			.ToList();
		var summaryTimelineEvents = ApplyTimelineDateFilter(
				dimensionTimelineEvents.Where(x => summaryLeadIds.Contains(x.LeadId)),
				input)
			.ToList();
		var automationFailures = summaryTimelineEvents
			.Where(x => x.EventType == AdmissionCrmTimelineEventType.AutomationFailed)
			.ToList();
		var automationFailureCount = automationFailures.Count;
		var recentAutomationFailures = automationFailures
			.OrderByDescending(x => x.OccurredAt)
			.ThenByDescending(x => x.Id)
			.Take(5)
			.ToList();
		var automationRunQueryable = await _automationRunRepository.GetQueryableAsync();
		var allAutomationRuns = await AsyncExecuter.ToListAsync(automationRunQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		var automationRuns = ApplyAutomationRunDateFilter(
				allAutomationRuns.Where(x =>
					(x.LeadId.HasValue && dimensionLeadIds.Contains(x.LeadId.Value)) ||
					(!x.LeadId.HasValue && hasAcademicYearWideScope)),
				input)
			.ToList();
		var cycleConfigurationQueryable = await _cycleConfigurationRepository.GetQueryableAsync();
		var cycleConfiguration = await AsyncExecuter.FirstOrDefaultAsync(cycleConfigurationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId));
		var firstResponseSlaHours = stages
			.FirstOrDefault(x => x.SystemStage == AdmissionInquiryStage.NewInquiry)
			?.SlaTargetHours ?? cycleConfiguration?.DefaultLeadSlaHours ?? 24;

		var totalLeadCount = leads.Count;
		var duplicateWarningCount = CountLikelyDuplicateLeads(leads);
		var examScheduledLeadCount = exams
			.Select(x => x.LeadId)
			.Distinct()
			.Count();
		var eligibilityHandoffCount = exams
			.Where(x => x.EligibilityDecision is not AdmissionCrmEligibilityDecision.Pending)
			.Select(x => x.LeadId)
			.Distinct()
			.Count();
		var staleLeadCount = leads.Count(x => x.Stage == AdmissionInquiryStage.Stale);
		var archivedLeadCount = leads.Count(x => x.Stage == AdmissionInquiryStage.Archived);
		var staleOrArchivedLeadCount = staleLeadCount + archivedLeadCount;
		return new AdmissionCrmAnalyticsSummaryDto
		{
			AcademicYearId = academicYearId,
			TrendFromDate = trendFromDate,
			TrendToDate = trendToDate,
			TotalLeadCount = totalLeadCount,
			ActiveLeadCount = leads.Count(x =>
				x.Stage is not AdmissionInquiryStage.Archived and not AdmissionInquiryStage.Rejected),
			ExamScheduledLeadCount = examScheduledLeadCount,
			EligibilityHandoffCount = eligibilityHandoffCount,
			StaleLeadCount = staleLeadCount,
			ArchivedLeadCount = archivedLeadCount,
			StaleOrArchivedLeadCount = staleOrArchivedLeadCount,
			DuplicateWarningCount = duplicateWarningCount,
			ExamScheduledRate = Rate(examScheduledLeadCount, totalLeadCount),
			EligibilityHandoffRate = Rate(eligibilityHandoffCount, totalLeadCount),
			StaleRate = Rate(staleLeadCount, totalLeadCount),
			ArchiveRate = Rate(archivedLeadCount, totalLeadCount),
			StaleOrArchivedRate = Rate(staleOrArchivedLeadCount, totalLeadCount),
			DuplicateWarningRate = Rate(duplicateWarningCount, totalLeadCount),
			AverageLeadScore = totalLeadCount == 0 ? 0 : Math.Round((decimal)leads.Average(x => x.LeadScore), 2),
			StageFunnel = BuildStageFunnel(leads, stages, totalLeadCount),
			ChannelPerformance = BuildChannelPerformance(leads, exams),
			SourcePerformance = BuildSourcePerformance(leads, exams),
			CampaignPerformance = BuildCampaignPerformance(leads, exams),
			StaffWorkload = BuildStaffWorkload(leads, tasks, exams, Clock.Now),
			TaskAnalytics = BuildTaskAnalytics(tasks, Clock.Now),
			SlaAnalytics = BuildSlaAnalytics(
				leads,
				dimensionTimelineEvents.Where(x => summaryLeadIds.Contains(x.LeadId)).ToList(),
				tasks,
				firstResponseSlaHours,
				new DateTimeOffset(Clock.Now)),
			ExamAnalytics = BuildExamAnalytics(exams, leads, slots),
			RegistrationAnalytics = BuildRegistrationAnalytics(registrations, totalLeadCount, Clock.Now),
			DocumentAnalytics = BuildDocumentAnalytics(checklists, documents, Clock.Now),
			FinalAdmissionAnalytics = BuildFinalAdmissionAnalytics(
				seatPools,
				seatOffers,
				seatReservations,
				waitlistEntries,
				admissionDecisions),
			FinancialAnalytics = BuildFinancialAnalytics(quotations, concessions, Clock.Now),
			HouseholdIdentityAnalytics = BuildHouseholdIdentityAnalytics(
				identityProfiles, guardianRelationships, applicantPeople),
			PolicyAnalytics = BuildPolicyAnalytics(policyPacks, policyAdoptions, policyEvaluations, policyExceptions, Clock.Now),
			RouteAnalytics = BuildRouteAnalytics(routeAssignments, routeAppeals),
			ActivationAnalytics = BuildActivationAnalytics(activations, identifiers),
			AutomationAnalytics = BuildAutomationAnalytics(
				automationRuns,
				automationFailureCount,
				recentAutomationFailures),
			DailyTrends = BuildDailyTrends(
				dimensionLeads,
				dimensionExams,
				dimensionTasks,
				dimensionTimelineEvents,
				trendFromDate,
				trendToDate)
		};
	}

	private static IEnumerable<AdmissionInquiryLead> ApplyDimensionFilters(
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

	private static IEnumerable<AdmissionInquiryLead> ApplyLeadDateFilter(
		IEnumerable<AdmissionInquiryLead> leads,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return leads.Where(x => IsInOptionalDateRange(x.CreationTime, input));
	}

	private static IEnumerable<AdmissionCrmEntranceExamRecord> ApplyExamDateFilter(
		IEnumerable<AdmissionCrmEntranceExamRecord> exams,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return exams.Where(x => IsInOptionalDateRange(x.ScheduledAt, input));
	}

	private static IEnumerable<AdmissionCrmFollowUpTask> ApplyTaskDateFilter(
		IEnumerable<AdmissionCrmFollowUpTask> tasks,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return tasks.Where(x => IsInOptionalDateRange(x.DueAt, input));
	}

	private static IEnumerable<AdmissionCrmTimelineEvent> ApplyTimelineDateFilter(
		IEnumerable<AdmissionCrmTimelineEvent> events,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return events.Where(x => IsInOptionalDateRange(x.OccurredAt, input));
	}

	private static IEnumerable<AdmissionCrmAutomationRun> ApplyAutomationRunDateFilter(
		IEnumerable<AdmissionCrmAutomationRun> runs,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return runs.Where(x => IsInOptionalDateRange(x.CompletedAt, input));
	}

	private static bool IsInOptionalDateRange(DateTime value, GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var date = value.Date;
		return (!input.FromDate.HasValue || date >= input.FromDate.Value.Date) &&
		       (!input.ToDate.HasValue || date <= input.ToDate.Value.Date);
	}

	private static bool IsInOptionalDateRange(DateTimeOffset value, GetAdmissionCrmAnalyticsSummaryInput input)
	{
		var date = value.UtcDateTime.Date;
		return (!input.FromDate.HasValue || date >= input.FromDate.Value.Date) &&
		       (!input.ToDate.HasValue || date <= input.ToDate.Value.Date);
	}

	private static (DateTime FromDate, DateTime ToDate) GetTrendDateRange(
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

	private static void ValidateAnalyticsInput(GetAdmissionCrmAnalyticsSummaryInput input)
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

	private static IReadOnlyList<AdmissionCrmDailyTrendDto> BuildDailyTrends(
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

	private static bool IsSameDate(DateTime value, DateTime date)
	{
		return value.Date == date.Date;
	}

	private static bool IsSameDate(DateTimeOffset value, DateTime date)
	{
		return value.UtcDateTime.Date == date.Date;
	}

	private static AdmissionCrmPolicyAnalyticsDto BuildPolicyAnalytics(
		IReadOnlyCollection<AdmissionCrmPolicyPack> packs,
		IReadOnlyCollection<AdmissionCrmPolicyAdoption> adoptions,
		IReadOnlyCollection<AdmissionCrmPolicyEvaluation> evaluations,
		IReadOnlyCollection<AdmissionCrmPolicyException> exceptions,
		DateTime now)
	{
		var latestEvaluations = evaluations
			.GroupBy(x => new { x.LeadId, x.PolicyAdoptionId, x.RuleCode })
			.Select(x => x.OrderByDescending(y => y.EvaluatedAt).ThenByDescending(y => y.Id).First())
			.ToList();
		return new AdmissionCrmPolicyAnalyticsDto
		{
			ActiveAdoptionCount = adoptions.Count(x => x.IsActive),
			EvaluationCount = latestEvaluations.Count,
			BlockingFailureCount = latestEvaluations.Count(x =>
				x.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired &&
				x.Status == AdmissionCrmPolicyEvaluationStatus.NotSatisfied),
			EvidencePendingCount = latestEvaluations.Count(x => x.Status == AdmissionCrmPolicyEvaluationStatus.EvidencePending),
			ApprovedExceptionCount = exceptions.Count(x => x.Status == AdmissionCrmPolicyExceptionStatus.Approved),
			SourceReviewOverdueCount = packs.Count(x => x.Status == AdmissionCrmPolicyPackStatus.Published && x.ReviewDueAt < now)
		};
	}

	private static AdmissionCrmRouteAnalyticsDto BuildRouteAnalytics(
		IReadOnlyCollection<AdmissionCrmRouteAssignment> assignments,
		IReadOnlyCollection<AdmissionCrmRouteAppeal> appeals)
	{
		var current = assignments.Where(x => x.IsPrimary).ToList();
		return new AdmissionCrmRouteAnalyticsDto
		{
			AssignedApplicantCount = current.Count,
			EligibleCount = current.Count(x => x.Status is AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved),
			PendingReviewCount = current.Count(x => x.Status == AdmissionCrmRouteEligibilityStatus.PendingReview),
			EvidencePendingCount = current.Count(x => x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending),
			RouteChangeCount = assignments.Count(x => x.PreviousAssignmentId.HasValue),
			AppealPendingCount = appeals.Count(x => x.Status is AdmissionCrmRouteAppealStatus.Requested or AdmissionCrmRouteAppealStatus.UnderReview),
			AppealApprovedCount = appeals.Count(x => x.Status == AdmissionCrmRouteAppealStatus.Approved),
			ApplicantsByRoute = current.GroupBy(x => x.RouteType.ToString()).ToDictionary(x => x.Key, x => x.Count())
		};
	}

	private static AdmissionCrmActivationAnalyticsDto BuildActivationAnalytics(
		IReadOnlyCollection<AdmissionCrmActivation> activations,
		IReadOnlyCollection<AdmissionCrmIdentifierAllocation> identifiers)
	{
		var completed = activations.Where(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady).ToList();
		return new AdmissionCrmActivationAnalyticsDto
		{
			RequestedCount = activations.Count,
			ApprovalPendingCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.ApprovalPending),
			FirstDayReadyCount = completed.Count,
			RepairRequiredCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.RepairRequired),
			CancellationPendingCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.CancellationPending),
			CancelledCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.Cancelled),
			FirstPassSuccessCount = completed.Count(x => x.AttemptCount == 1),
			FirstPassSuccessRate = Rate(completed.Count(x => x.AttemptCount == 1), completed.Count),
			AverageAttempts = activations.Count == 0 ? 0 : Math.Round((decimal)activations.Average(x => x.AttemptCount), 2),
			CommittedIdentifierCount = identifiers.Count(x => x.Status == AdmissionCrmIdentifierStatus.Committed),
			VoidedIdentifierCount = identifiers.Count(x => x.Status == AdmissionCrmIdentifierStatus.Void)
		};
	}

	private static AdmissionCrmFinalAdmissionAnalyticsDto BuildFinalAdmissionAnalytics(
		IReadOnlyCollection<AdmissionCrmSeatPool> seatPools,
		IReadOnlyCollection<AdmissionCrmSeatOffer> offers,
		IReadOnlyCollection<AdmissionCrmSeatReservation> reservations,
		IReadOnlyCollection<AdmissionCrmWaitlistEntry> waitlistEntries,
		IReadOnlyCollection<AdmissionCrmAdmissionDecision> decisions)
	{
		var currentDecisions = decisions
			.GroupBy(x => x.LeadId)
			.Select(x => x
				.OrderByDescending(y => y.DecidedAt ?? y.CreationTime)
				.ThenByDescending(y => y.Id)
				.First())
			.ToList();
		var responseEligibleOffers = offers.Count(x => x.Status is
			AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened or
			AdmissionCrmSeatOfferStatus.Accepted or
			AdmissionCrmSeatOfferStatus.Declined or
			AdmissionCrmSeatOfferStatus.Expired or
			AdmissionCrmSeatOfferStatus.Cancelled);
		var respondedOffers = offers.Count(x => x.Status is
			AdmissionCrmSeatOfferStatus.Accepted or AdmissionCrmSeatOfferStatus.Declined);
		var acceptedOffers = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted);
		var promotedWaitlistEntries = waitlistEntries.Count(x => x.OfferId.HasValue);
		var decidedApplicants = currentDecisions.Count(x =>
			x.Status != AdmissionCrmAdmissionDecisionStatus.Pending);
		var occupiedSeats = seatPools.Sum(x => x.OccupiedSeats);
		var approvedCapacity = seatPools.Sum(x => x.ApprovedCapacity);

		return new AdmissionCrmFinalAdmissionAnalyticsDto
		{
			SeatPoolCount = seatPools.Count,
			ApprovedCapacity = approvedCapacity,
			ReservedSeatCount = seatPools.Sum(x => x.ReservedSeats),
			AcceptedSeatCount = seatPools.Sum(x => x.AcceptedSeats),
			ActivatedSeatCount = seatPools.Sum(x => x.ActivatedSeats),
			RemainingSeatCount = seatPools.Sum(x => x.RemainingCapacity),
			OverCapacityPoolCount = seatPools.Count(x => x.IsOverCapacity),
			DraftOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Draft),
			ApprovalPendingOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.ApprovalPending),
			IssuedOfferCount = offers.Count(x => x.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened),
			AcceptedOfferCount = acceptedOffers,
			DeclinedOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Declined),
			ExpiredOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Expired),
			CancelledOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Cancelled),
			ActiveWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Active),
			PromotedWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Promoted),
			AcceptedWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Accepted),
			CapacityOverrideCount = reservations.Count(x => x.CapacityOverrideUsed),
			ApprovedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Approved),
			RejectedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Rejected),
			WaitlistedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Waitlisted),
			CancelledDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled),
			SeatOccupancyRate = Rate(occupiedSeats, approvedCapacity),
			OfferResponseRate = Rate(respondedOffers, responseEligibleOffers),
			OfferAcceptanceRate = Rate(acceptedOffers, responseEligibleOffers),
			WaitlistPromotionRate = Rate(promotedWaitlistEntries, waitlistEntries.Count),
			AdmissionApprovalRate = Rate(
				currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Approved),
				decidedApplicants)
		};
	}

	private static AdmissionCrmFinancialAnalyticsDto BuildFinancialAnalytics(
		IReadOnlyCollection<AdmissionCrmAdmissionQuotation> quotations,
		IReadOnlyCollection<AdmissionCrmConcessionRequest> concessions,
		DateTime at)
	{
		var issued = quotations
			.Where(x => x.Status == AdmissionCrmQuotationStatus.Issued)
			.ToList();
		var confirmedCount = issued.Count(x => x.FinanceState == AdmissionCrmFinanceState.Confirmed);
		var satisfiedCount = issued.Count(x => x.IsGateSatisfiedAt(at));
		return new AdmissionCrmFinancialAnalyticsDto
		{
			IssuedQuotationCount = issued.Count,
			FinanceConfirmedCount = confirmedCount,
			PendingGateCount = issued.Count - satisfiedCount,
			FailedOrDisputedCount = issued.Count(x => x.FinanceState is
				AdmissionCrmFinanceState.Failed or AdmissionCrmFinanceState.Reversed or AdmissionCrmFinanceState.Disputed),
			GateOverrideCount = issued.Count(x => x.GateOverrideUsed),
			GrossQuotedAmount = issued.Sum(x => x.GrossAmount),
			ConcessionAmount = issued.Sum(x => x.ConcessionAmount),
			NetQuotedAmount = issued.Sum(x => x.NetAmount),
			ConfirmedAmount = issued.Sum(x => x.ConfirmedAmount),
			OutstandingAmount = issued.Sum(x => Math.Max(0, x.NetAmount - x.ConfirmedAmount)),
			ConcessionRequestCount = concessions.Count,
			ConcessionApprovalPendingCount = concessions.Count(x => x.Status == AdmissionCrmConcessionStatus.ApprovalPending),
			FinanceAcknowledgedConcessionCount = concessions.Count(x => x.Status == AdmissionCrmConcessionStatus.FinanceAcknowledged),
			FinanceAcknowledgedConcessionAmount = concessions
				.Where(x => x.Status == AdmissionCrmConcessionStatus.FinanceAcknowledged)
				.Sum(x => x.ApprovedAmount),
			FinanceConfirmationRate = Rate(confirmedCount, issued.Count),
			PaymentGateSatisfactionRate = Rate(satisfiedCount, issued.Count)
		};
	}

	private static AdmissionCrmHouseholdIdentityAnalyticsDto BuildHouseholdIdentityAnalytics(
		IReadOnlyCollection<AdmissionCrmApplicantIdentityProfile> profiles,
		IReadOnlyCollection<AdmissionCrmGuardianRelationship> guardians,
		IReadOnlyCollection<AdmissionCrmPerson> applicantPeople)
	{
		var reusedHouseholds = profiles.GroupBy(x => x.HouseholdId)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToHashSet();
		var profilesInReusedHouseholds = profiles.Count(x => reusedHouseholds.Contains(x.HouseholdId));
		var readyCount = profiles.Count(x => x.Status is
			AdmissionCrmIdentityResolutionStatus.Ready or AdmissionCrmIdentityResolutionStatus.Overridden);
		return new AdmissionCrmHouseholdIdentityAnalyticsDto
		{
			ProfileCount = profiles.Count,
			ReadyCount = readyCount,
			PendingReviewCount = profiles.Count(x => x.Status is
				AdmissionCrmIdentityResolutionStatus.Unresolved or AdmissionCrmIdentityResolutionStatus.PendingReview),
			ConflictCount = profiles.Count(x => x.Status == AdmissionCrmIdentityResolutionStatus.Conflict),
			OverrideCount = profiles.Count(x => x.OverrideUsed),
			ReusedHouseholdCount = reusedHouseholds.Count,
			GuardianRelationshipCount = guardians.Count,
			MissingAccountableContactCount = profiles.Count(x => !x.AccountableContactReady),
			MissingIdentityConsentCount = profiles.Count(x => !x.IdentityConsentReady),
			AccountLinkPendingCount = profiles.Count(x => !x.AccountLinkReady),
			AadhaarProvidedCount = applicantPeople.Count(x => x.AadhaarStatus != IdentityVerificationStatus.NotProvided),
			PenReadyCount = applicantPeople.Count(x => x.PenStatus == IdentityVerificationStatus.Verified),
			ApaarReadyCount = applicantPeople.Count(x => x.ApaarStatus == IdentityVerificationStatus.Verified),
			IdentityReadinessRate = Rate(readyCount, profiles.Count),
			HouseholdReuseRate = Rate(profilesInReusedHouseholds, profiles.Count)
		};
	}

	private static AdmissionCrmAutomationAnalyticsDto BuildAutomationAnalytics(
		IReadOnlyCollection<AdmissionCrmAutomationRun> runs,
		int failureCount,
		IReadOnlyCollection<AdmissionCrmTimelineEvent> recentFailures)
	{
		return new AdmissionCrmAutomationAnalyticsDto
		{
			RunCount = runs.Count,
			FailedRunCount = runs.Count(x => x.Status == AdmissionCrmAutomationRunStatus.CompletedWithFailures),
			FailureCount = failureCount,
			RecentRuns = runs
				.OrderByDescending(x => x.CompletedAt)
				.ThenByDescending(x => x.Id)
				.Take(5)
				.Select(x => new AdmissionCrmAutomationRunDto
				{
					Id = x.Id,
					RunType = x.RunType,
					Status = x.Status,
					Trigger = x.Trigger,
					Stage = x.Stage,
					LeadId = x.LeadId,
					StartedAt = x.StartedAt,
					CompletedAt = x.CompletedAt,
					EvaluatedLeadCount = x.EvaluatedLeadCount,
					MatchedRuleCount = x.MatchedRuleCount,
					CreatedTaskCount = x.CreatedTaskCount,
					SentMessageCount = x.SentMessageCount,
					SkippedExistingTaskCount = x.SkippedExistingTaskCount,
					SkippedExistingMessageCount = x.SkippedExistingMessageCount,
					FailedActionCount = x.FailedActionCount,
					MarkedStaleCount = x.MarkedStaleCount,
					ArchivedCount = x.ArchivedCount,
					Details = x.Details
				})
				.ToList(),
			RecentFailures = recentFailures
				.Select(x => new AdmissionCrmAutomationFailureDto
				{
					EventId = x.Id,
					LeadId = x.LeadId,
					Title = x.Title,
					Details = x.Details,
					OccurredAt = x.OccurredAt
				})
				.ToList()
		};
	}

	private static IReadOnlyList<AdmissionCrmStageFunnelDto> BuildStageFunnel(
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
					StageName = stageNameBySystemStage.GetValueOrDefault(stage, ToDisplayLabel(stage.ToString())),
					LeadCount = leadCount,
					ShareOfLeads = Rate(leadCount, totalLeadCount)
				};
			})
			.ToList();
	}

	private static IReadOnlyList<AdmissionCrmChannelPerformanceDto> BuildChannelPerformance(
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
					ChannelName = ToDisplayLabel(channel.ToString()),
					LeadCount = channelLeads.Count,
					ExamScheduledCount = channelExams.Count,
					EligibleCount = eligibleCount,
					WaitlistedCount = channelExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted),
					RejectedCount = channelExams.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
					EligibilityRate = Rate(eligibleCount, channelLeads.Count)
				};
			})
			.Where(x => x.LeadCount > 0 || x.ExamScheduledCount > 0)
			.ToList();
	}

	private static IReadOnlyList<AdmissionCrmSourcePerformanceDto> BuildSourcePerformance(
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
					EligibilityRate = Rate(eligibleCount, sourceLeads.Count)
				};
			})
			.ToList();
	}

	private static IReadOnlyList<AdmissionCrmCampaignPerformanceDto> BuildCampaignPerformance(
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
					EligibilityRate = Rate(eligibleCount, campaignLeads.Count),
					AverageLeadScore = Math.Round((decimal)campaignLeads.Average(x => x.LeadScore), 2)
				};
			})
			.ToList();
	}

	private static AdmissionCrmTaskAnalyticsDto BuildTaskAnalytics(
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

	private static AdmissionCrmSlaAnalyticsDto BuildSlaAnalytics(
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
			FirstResponseRate = Rate(firstResponseCount, leads.Count),
			FirstResponseSlaMetRate = Rate(firstResponseSlaMetCount, firstResponseCount),
			FirstResponseSlaMissedRate = Rate(firstResponseSlaMissedCount, leads.Count),
			AverageFirstResponseHours = responseHours.Count == 0
				? 0
				: Math.Round((decimal)responseHours.Average(), 2),
			FollowUpSlaMissedCount = followUpSlaMissedCount,
			FollowUpSlaMissedRate = Rate(followUpSlaMissedCount, leads.Count)
		};
	}

	private static bool IsFirstResponseEvent(AdmissionCrmTimelineEvent timelineEvent)
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

	private static IReadOnlyList<AdmissionCrmStaffWorkloadDto> BuildStaffWorkload(
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

	private static AdmissionCrmExamAnalyticsDto BuildExamAnalytics(
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmExamSlot> slots)
	{
		var scheduledCount = exams.Count;
		var presentCount = exams.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Present);
		var passCount = exams.Count(x => x.ExamStatus == EntranceExamStatusEnum.Pass);
		var eligibleCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);
		var waitlistedCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted);
		var rejectedCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected);
		var resultedCount = exams.Count(x =>
			x.ExamStatus is EntranceExamStatusEnum.Pass or EntranceExamStatusEnum.Fail);
		var percentages = exams
			.Where(x => x.Percentage.HasValue)
			.Select(x => x.Percentage!.Value)
			.ToList();
		var totalCapacity = slots.Sum(x => x.StudentLimit);
		var assignmentsBySlot = exams.GroupBy(x => x.EntranceExamSlotId).ToDictionary(x => x.Key, x => x.Count());

		return new AdmissionCrmExamAnalyticsDto
		{
			ScheduledCount = scheduledCount,
			PresentCount = presentCount,
			AbsentCount = exams.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent),
			NotMarkedCount = exams.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked),
			PassCount = passCount,
			FailCount = exams.Count(x => x.ExamStatus == EntranceExamStatusEnum.Fail),
			EligibleCount = eligibleCount,
			WaitlistedCount = waitlistedCount,
			RejectedCount = rejectedCount,
			MeritRankedCount = exams.Count(x => x.MeritRank.HasValue),
			CapacityOverrideCount = exams.Count(x => x.CapacityOverrideUsed),
			EligibilityOverrideCount = exams.Count(x => x.EligibilityOverrideUsed),
			AttendanceRate = Rate(presentCount, scheduledCount),
			PassRate = Rate(passCount, resultedCount),
			EligibilityRate = Rate(eligibleCount, scheduledCount),
			WaitlistRate = Rate(waitlistedCount, scheduledCount),
			RejectionRate = Rate(rejectedCount, scheduledCount),
			AveragePercentage = percentages.Count == 0 ? 0 : Math.Round((decimal)percentages.Average(), 2),
			HighestPercentage = percentages.Count == 0 ? null : Math.Round(percentages.Max(), 2),
			LowestPercentage = percentages.Count == 0 ? null : Math.Round(percentages.Min(), 2),
			SlotCount = slots.Count,
			TotalSlotCapacity = totalCapacity,
			OverCapacitySlotCount = slots.Count(x => assignmentsBySlot.GetValueOrDefault(x.Id) > x.StudentLimit),
			SlotUtilizationRate = Rate(scheduledCount, totalCapacity),
			ClassPerformance = BuildExamClassPerformance(exams, leads)
		};
	}

	private static AdmissionCrmRegistrationAnalyticsDto BuildRegistrationAnalytics(
		IReadOnlyCollection<AdmissionCrmRegistrationApplication> registrations,
		int totalLeadCount,
		DateTime now)
	{
		var issuedCount = registrations.Count;
		var openedCount = registrations.Count(x => x.OpenedAt.HasValue);
		var submittedCount = registrations.Count(x => x.SubmittedAt.HasValue);
		var verifiedCount = registrations.Count(x => x.VerifiedAt.HasValue);
		var admissionReadyCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.AdmissionReady);
		var abandonedCount = registrations.Count(x =>
			x.Status is AdmissionCrmRegistrationStatus.Issued
				or AdmissionCrmRegistrationStatus.Opened
				or AdmissionCrmRegistrationStatus.Started
				or AdmissionCrmRegistrationStatus.PartiallyCompleted &&
			(now.Date - (x.LastSavedAt ?? x.IssuedAt).Date).Days >= 7);
		return new AdmissionCrmRegistrationAnalyticsDto
		{
			IssuedCount = issuedCount,
			OpenedCount = openedCount,
			StartedCount = registrations.Count(x => x.LastSavedAt.HasValue),
			SubmittedCount = submittedCount,
			VerifiedCount = verifiedCount,
			AdmissionReadyCount = admissionReadyCount,
			ExpiredCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.Expired ||
				x.LinkExpiresAt <= now && x.Status is not AdmissionCrmRegistrationStatus.Verified and not AdmissionCrmRegistrationStatus.AdmissionReady),
			CorrectionPendingCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection),
			AbandonedCount = abandonedCount,
			OpenRate = Rate(openedCount, issuedCount),
			CompletionRate = Rate(submittedCount, issuedCount),
			VerificationRate = Rate(verifiedCount, submittedCount),
			AdmissionYieldRate = Rate(admissionReadyCount, totalLeadCount),
			AverageCompletionPercentage = issuedCount == 0 ? 0 : Math.Round(registrations.Average(x => x.CompletionPercentage), 2)
		};
	}

	private static AdmissionCrmDocumentAnalyticsDto BuildDocumentAnalytics(
		IReadOnlyCollection<AdmissionCrmDocumentChecklistAssignment> checklists,
		IReadOnlyCollection<AdmissionCrmApplicantDocument> documents,
		DateTime now)
	{
		var currentDocuments = documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First())
			.ToList();
		var decidedDocuments = currentDocuments.Where(x => x.DecisionAt.HasValue).ToList();
		var reviewAges = decidedDocuments
			.Select(x => Math.Max(0, (x.DecisionAt!.Value - x.UploadedAt).TotalDays))
			.Concat(currentDocuments
				.Where(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview)
				.Select(x => Math.Max(0, (now - x.UploadedAt).TotalDays)))
			.ToList();
		return new AdmissionCrmDocumentAnalyticsDto
		{
			AssignedChecklistCount = checklists.Count,
			UploadedCount = currentDocuments.Count,
			UnderReviewCount = currentDocuments.Count(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview),
			VerifiedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Verified),
			RejectedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Rejected),
			WaivedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Waived),
			OriginalSeenCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen),
			ReadinessConfirmedCount = checklists.Count(x => x.ReadinessConfirmedAt.HasValue),
			ReadinessOverrideCount = checklists.Count(x => x.ReadinessOverridden),
			RejectionRate = Rate(currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Rejected), currentDocuments.Count),
			AverageReviewAgeDays = reviewAges.Count == 0 ? 0 : Math.Round((decimal)reviewAges.Average(), 2)
		};
	}

	private static IReadOnlyList<AdmissionCrmExamClassPerformanceDto> BuildExamClassPerformance(
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionInquiryLead> leads)
	{
		var leadById = leads.ToDictionary(x => x.Id);

		return exams
			.GroupBy(exam =>
			{
				leadById.TryGetValue(exam.LeadId, out var lead);
				var className = string.IsNullOrWhiteSpace(lead?.DesiredClassName)
					? lead?.TargetStandardId?.ToString("D") ?? "Unassigned"
					: lead.DesiredClassName;
				return new
				{
					TargetStandardId = lead?.TargetStandardId,
					ClassName = className
				};
			})
			.Select(group =>
			{
				var scheduledCount = group.Count();
				var presentCount = group.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Present);
				var passCount = group.Count(x => x.ExamStatus == EntranceExamStatusEnum.Pass);
				var eligibleCount = group.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);
				var resultedCount = group.Count(x =>
					x.ExamStatus is EntranceExamStatusEnum.Pass or EntranceExamStatusEnum.Fail);
				var percentages = group
					.Where(x => x.Percentage.HasValue)
					.Select(x => x.Percentage!.Value)
					.ToList();

				return new AdmissionCrmExamClassPerformanceDto
				{
					TargetStandardId = group.Key.TargetStandardId,
					ClassName = group.Key.ClassName,
					ScheduledCount = scheduledCount,
					PresentCount = presentCount,
					AbsentCount = group.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent),
					PassCount = passCount,
					FailCount = group.Count(x => x.ExamStatus == EntranceExamStatusEnum.Fail),
					EligibleCount = eligibleCount,
					WaitlistedCount = group.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted),
					RejectedCount = group.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
					MeritRankedCount = group.Count(x => x.MeritRank.HasValue),
					AttendanceRate = Rate(presentCount, scheduledCount),
					PassRate = Rate(passCount, resultedCount),
					EligibilityRate = Rate(eligibleCount, scheduledCount),
					AveragePercentage = percentages.Count == 0
						? 0
						: Math.Round((decimal)percentages.Average(), 2)
				};
			})
			.OrderByDescending(x => x.ScheduledCount)
			.ThenBy(x => x.ClassName)
			.ToList();
	}

	private static int CountLikelyDuplicateLeads(IReadOnlyCollection<AdmissionInquiryLead> leads)
	{
		var duplicateLeadIds = new HashSet<Guid>();
		foreach (var group in leads
			         .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedPrimaryContactNo))
			         .GroupBy(x => x.NormalizedPrimaryContactNo)
			         .Where(x => x.Count() > 1))
		{
			foreach (var lead in group)
			{
				duplicateLeadIds.Add(lead.Id);
			}
		}

		foreach (var group in leads
			         .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedEmail))
			         .GroupBy(x => x.NormalizedEmail)
			         .Where(x => x.Count() > 1))
		{
			foreach (var lead in group)
			{
				duplicateLeadIds.Add(lead.Id);
			}
		}

		foreach (var group in leads
			         .Where(HasDuplicateProfile)
			         .GroupBy(x => new
			         {
				         FirstName = NormalizeText(x.FirstName),
				         AdmissionTarget = GetDuplicateTargetKey(x)
			         })
			         .Where(x => x.Count() > 1))
		{
			var groupedLeads = group.ToList();
			foreach (var lead in groupedLeads)
			{
				if (groupedLeads.Any(candidate =>
					    candidate.Id != lead.Id &&
					    AreLastNamesCompatible(candidate.LastName, lead.LastName) &&
					    HasDuplicateIdentityMatch(candidate, lead)))
				{
					duplicateLeadIds.Add(lead.Id);
				}
			}
		}

		return duplicateLeadIds.Count;
	}

	private static bool HasDuplicateProfile(AdmissionInquiryLead lead)
	{
		return
			NormalizeText(lead.FirstName) != null &&
			(NormalizeText(lead.ParentGuardianName) != null || lead.DateOfBirth.HasValue) &&
			GetDuplicateTargetKey(lead) != null;
	}

	private static string? GetDuplicateTargetKey(AdmissionInquiryLead lead)
	{
		if (lead.TargetStandardId.HasValue)
		{
			return "standard:" + lead.TargetStandardId.Value.ToString("D");
		}

		var desiredClassName = NormalizeText(lead.DesiredClassName);
		return desiredClassName == null ? null : "class:" + desiredClassName;
	}

	private static bool AreLastNamesCompatible(string? firstLastName, string? secondLastName)
	{
		var normalizedFirstLastName = NormalizeText(firstLastName);
		var normalizedSecondLastName = NormalizeText(secondLastName);
		return
			normalizedFirstLastName == null ||
			normalizedSecondLastName == null ||
			normalizedFirstLastName == normalizedSecondLastName;
	}

	private static bool HasDuplicateIdentityMatch(AdmissionInquiryLead first, AdmissionInquiryLead second)
	{
		var firstParent = NormalizeText(first.ParentGuardianName);
		var secondParent = NormalizeText(second.ParentGuardianName);
		if (firstParent != null && firstParent == secondParent)
		{
			return true;
		}

		return first.DateOfBirth.HasValue &&
		       second.DateOfBirth.HasValue &&
		       first.DateOfBirth.Value.Date == second.DateOfBirth.Value.Date;
	}

	private static string? NormalizeText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return string.Join(
			" ",
			value
				.Trim()
				.ToLowerInvariant()
				.Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	private static decimal Rate(int part, int total)
	{
		return total == 0 ? 0 : Math.Round(part * 100m / total, 2);
	}

	private static string ToDisplayLabel(string value)
	{
		return string.Concat(value.Select((character, index) =>
			index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
	}
}
