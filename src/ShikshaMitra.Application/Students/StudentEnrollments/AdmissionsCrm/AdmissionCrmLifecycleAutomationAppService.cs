using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[RemoteService(false)]
public class AdmissionCrmLifecycleAutomationAppService : ApplicationService, IAdmissionCrmLifecycleAutomationAppService
{
	private const int LeadBatchSize = 250;

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmApplicantDocument, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _checklistRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IAdmissionCrmLeadAppService _leadAppService;
	private readonly IAdmissionCrmFinalAdmissionAppService _finalAdmissionAppService;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public AdmissionCrmLifecycleAutomationAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IAdmissionCrmLeadAppService leadAppService,
		IAdmissionCrmFinalAdmissionAppService finalAdmissionAppService,
		StudentScopeAccessService studentScopeAccessService,
		IUnitOfWorkManager unitOfWorkManager)
	{
		_leadRepository = leadRepository;
		_registrationRepository = registrationRepository;
		_documentRepository = documentRepository;
		_checklistRepository = checklistRepository;
		_examRepository = examRepository;
		_quotationRepository = quotationRepository;
		_offerRepository = offerRepository;
		_activationRepository = activationRepository;
		_leadAppService = leadAppService;
		_finalAdmissionAppService = finalAdmissionAppService;
		_studentScopeAccessService = studentScopeAccessService;
		_unitOfWorkManager = unitOfWorkManager;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmLifecycleAutomationResultDto> RunAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(RunAsync));
		return await RunCoreAsync(academicYearId, enforceUserScope: true);
	}

	public Task<AdmissionCrmLifecycleAutomationResultDto> RunFromAutomationAsync(Guid academicYearId) =>
		RunCoreAsync(academicYearId, enforceUserScope: false);

	private async Task<AdmissionCrmLifecycleAutomationResultDto> RunCoreAsync(
		Guid academicYearId,
		bool enforceUserScope)
	{
		var tenantId = CurrentTenant.Id;
		var result = new AdmissionCrmLifecycleAutomationResultDto
		{
			AcademicYearId = academicYearId,
			EvaluatedLeadCount = await CountActiveLeadsAsync(
				academicYearId,
				tenantId,
				enforceUserScope)
		};
		var triggerCounts = new Dictionary<AdmissionCrmJourneyTrigger, int>();
		var now = Clock.Now;
		var skipCount = 0;

		while (skipCount < result.EvaluatedLeadCount)
		{
			var processedCount = await ProcessBatchAsync(
				academicYearId,
				tenantId,
				enforceUserScope,
				skipCount,
				now,
				result,
				triggerCounts);
			if (processedCount == 0)
			{
				break;
			}
			skipCount += processedCount;
		}

		result.TriggerCounts = triggerCounts;
		using (var expiryUnitOfWork = _unitOfWorkManager.Begin(
			requiresNew: true,
			isTransactional: false))
		{
			var expiry = enforceUserScope
				? await _finalAdmissionAppService.RunOfferExpiryAsync(academicYearId)
				: await _finalAdmissionAppService.RunOfferExpiryFromAutomationAsync(academicYearId);
			result.ExpiredOfferCount = expiry.ExpiredCount;
			result.OfferExpiryFailureCount = expiry.FailedCount;
			await expiryUnitOfWork.CompleteAsync();
		}
		return result;
	}

	private async Task<int> CountActiveLeadsAsync(
		Guid academicYearId,
		Guid? tenantId,
		bool enforceUserScope)
	{
		using var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var query = await BuildActiveLeadQueryAsync(academicYearId, tenantId, enforceUserScope);
		var count = await AsyncExecuter.CountAsync(query);
		await unitOfWork.CompleteAsync();
		return count;
	}

	private async Task<int> ProcessBatchAsync(
		Guid academicYearId,
		Guid? tenantId,
		bool enforceUserScope,
		int skipCount,
		DateTime now,
		AdmissionCrmLifecycleAutomationResultDto result,
		IDictionary<AdmissionCrmJourneyTrigger, int> triggerCounts)
	{
		using var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var leadQuery = await BuildActiveLeadQueryAsync(academicYearId, tenantId, enforceUserScope);
		var leadIds = await AsyncExecuter.ToListAsync(
			leadQuery
				.OrderBy(lead => lead.Id)
				.Skip(skipCount)
				.Take(LeadBatchSize)
				.Select(lead => lead.Id));
		if (leadIds.Count == 0)
		{
			await unitOfWork.CompleteAsync();
			return 0;
		}

		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		var registrations = await AsyncExecuter.ToListAsync(registrationQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var documentQueryable = await _documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var checklistQueryable = await _checklistRepository.GetQueryableAsync();
		var checklists = await AsyncExecuter.ToListAsync(checklistQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var examQueryable = await _examRepository.GetQueryableAsync();
		var exams = await AsyncExecuter.ToListAsync(examQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var quotationQueryable = await _quotationRepository.GetQueryableAsync();
		var quotations = await AsyncExecuter.ToListAsync(quotationQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var offerQueryable = await _offerRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(offerQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));
		var activationQueryable = await _activationRepository.GetQueryableAsync();
		var activations = await AsyncExecuter.ToListAsync(activationQueryable.Where(item =>
			item.TenantId == tenantId &&
			item.AcademicYearId == academicYearId &&
			leadIds.Contains(item.LeadId)));

		var triggers = BuildTriggers(
			registrations,
			documents,
			checklists,
			exams,
			quotations,
			offers,
			activations,
			now);
		var batchTriggeredLeadIds = new HashSet<Guid>();
		foreach (var (trigger, matchingLeadIds) in triggers)
		{
			foreach (var leadId in matchingLeadIds)
			{
				var input = new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = trigger,
					LeadId = leadId
				};
				var journey = enforceUserScope
					? await _leadAppService.RunJourneyRulesAsync(academicYearId, input)
					: await _leadAppService.RunJourneyRulesFromAutomationAsync(academicYearId, input);
				result.CreatedTaskCount += journey.CreatedTaskCount;
				result.SentMessageCount += journey.SentMessageCount;
				batchTriggeredLeadIds.Add(leadId);
				triggerCounts[trigger] =
					(triggerCounts.TryGetValue(trigger, out var count) ? count : 0) + 1;
			}
		}
		result.TriggeredLeadCount += batchTriggeredLeadIds.Count;
		await unitOfWork.CompleteAsync();
		return leadIds.Count;
	}

	private async Task<IQueryable<AdmissionInquiryLead>> BuildActiveLeadQueryAsync(
		Guid academicYearId,
		Guid? tenantId,
		bool enforceUserScope)
	{
		var queryable = await _leadRepository.GetQueryableAsync();
		var query = queryable.Where(lead =>
			lead.TenantId == tenantId &&
			lead.AcademicYearId == academicYearId &&
			lead.Stage != AdmissionInquiryStage.Archived &&
			lead.Stage != AdmissionInquiryStage.Rejected);
		if (enforceUserScope)
		{
			query = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		}
		return query;
	}

	private static Dictionary<AdmissionCrmJourneyTrigger, HashSet<Guid>> BuildTriggers(
		IReadOnlyCollection<AdmissionCrmRegistrationApplication> registrations,
		IReadOnlyCollection<AdmissionCrmApplicantDocument> documents,
		IReadOnlyCollection<AdmissionCrmDocumentChecklistAssignment> checklists,
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionCrmAdmissionQuotation> quotations,
		IReadOnlyCollection<AdmissionCrmSeatOffer> offers,
		IReadOnlyCollection<AdmissionCrmActivation> activations,
		DateTime now)
	{
		var currentDocuments = CurrentDocuments(documents).ToList();
		var expiringDocumentLeadIds =
			AdmissionCrmDocumentExpiryReminderPolicy.GetExpiringLeadIds(checklists, documents, now);
		return new Dictionary<AdmissionCrmJourneyTrigger, HashSet<Guid>>
		{
			[AdmissionCrmJourneyTrigger.RegistrationIssued] = registrations
				.Where(item => item.Status == AdmissionCrmRegistrationStatus.Issued)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationIncomplete] = registrations
				.Where(item =>
					IsIncomplete(item.Status) &&
					(now - (item.LastSavedAt ?? item.IssuedAt)).TotalDays >= 7)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationExpiring] = registrations
				.Where(item =>
					IsIncomplete(item.Status) &&
					item.LinkExpiresAt <= now.AddDays(3))
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationCorrectionRequested] = registrations
				.Where(item =>
					item.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationSubmitted] = registrations
				.Where(item =>
					item.Status is
						AdmissionCrmRegistrationStatus.Submitted or
						AdmissionCrmRegistrationStatus.Resubmitted)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationVerified] = registrations
				.Where(item =>
					item.Status is
						AdmissionCrmRegistrationStatus.Verified or
						AdmissionCrmRegistrationStatus.AdmissionReady)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentUploaded] = currentDocuments
				.Where(item => item.Status == AdmissionCrmApplicantDocumentStatus.Uploaded)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentRejected] = currentDocuments
				.Where(item => item.Status == AdmissionCrmApplicantDocumentStatus.Rejected)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentPending] = currentDocuments
				.Where(item =>
					(item.Status is
						AdmissionCrmApplicantDocumentStatus.Uploaded or
						AdmissionCrmApplicantDocumentStatus.UnderReview) &&
					(now - item.UploadedAt).TotalDays >= 2)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentExpiring] = expiringDocumentLeadIds,
			[AdmissionCrmJourneyTrigger.FeeFollowUp] = quotations
				.Where(item =>
					item.Status == AdmissionCrmQuotationStatus.Issued &&
					!item.IsGateSatisfiedAt(now))
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.OfferIssued] = offers
				.Where(item =>
					item.Status is
						AdmissionCrmSeatOfferStatus.Issued or
						AdmissionCrmSeatOfferStatus.Opened)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.OfferAccepted] = offers
				.Where(item => item.Status == AdmissionCrmSeatOfferStatus.Accepted)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.ActivationWelcome] = activations
				.Where(item => item.Status == AdmissionCrmActivationStatus.FirstDayReady)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.ExamNoShow] = exams
				.Where(item =>
					item.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ||
					item.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked &&
					item.ExamEndTime.UtcDateTime < now)
				.Select(item => item.LeadId)
				.ToHashSet(),
			[AdmissionCrmJourneyTrigger.AdmissionReady] = checklists
				.Where(item => item.ReadinessConfirmedAt.HasValue)
				.Select(item => item.LeadId)
				.ToHashSet()
		};
	}

	private static bool IsIncomplete(AdmissionCrmRegistrationStatus status) =>
		status is
			AdmissionCrmRegistrationStatus.Issued or
			AdmissionCrmRegistrationStatus.Opened or
			AdmissionCrmRegistrationStatus.Started or
			AdmissionCrmRegistrationStatus.PartiallyCompleted or
			AdmissionCrmRegistrationStatus.ReturnedForCorrection;

	private static IEnumerable<AdmissionCrmApplicantDocument> CurrentDocuments(
		IEnumerable<AdmissionCrmApplicantDocument> documents) =>
		documents
			.Where(item => item.SupersededAt == null)
			.GroupBy(item => new { item.ChecklistAssignmentId, item.RequirementKey })
			.Select(group => group
				.OrderByDescending(document => document.Version)
				.First());
}
