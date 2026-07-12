using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[RemoteService(false)]
public class AdmissionCrmLifecycleAutomationAppService : ApplicationService, IAdmissionCrmLifecycleAutomationAppService
{
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmApplicantDocument, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _checklistRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IAdmissionCrmLeadAppService _leadAppService;
	private readonly IAdmissionCrmFinalAdmissionAppService _finalAdmissionAppService;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmLifecycleAutomationAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IAdmissionCrmLeadAppService leadAppService,
		IAdmissionCrmFinalAdmissionAppService finalAdmissionAppService,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_registrationRepository = registrationRepository;
		_documentRepository = documentRepository;
		_checklistRepository = checklistRepository;
		_examRepository = examRepository;
		_leadAppService = leadAppService;
		_finalAdmissionAppService = finalAdmissionAppService;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public async Task<AdmissionCrmLifecycleAutomationResultDto> RunAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RunAsync));
		return await RunCoreAsync(academicYearId);
	}

	public Task<AdmissionCrmLifecycleAutomationResultDto> RunFromAutomationAsync(Guid academicYearId) =>
		RunCoreAsync(academicYearId);

	private async Task<AdmissionCrmLifecycleAutomationResultDto> RunCoreAsync(Guid academicYearId)
	{
		var tenantId = CurrentTenant.Id;
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var leads = await AsyncExecuter.ToListAsync(leadQueryable.Where(x =>
			x.TenantId == tenantId && x.AcademicYearId == academicYearId));
		var activeLeadIds = leads
			.Where(x => x.Stage is not AdmissionInquiryStage.Archived and not AdmissionInquiryStage.Rejected)
			.Select(x => x.Id)
			.ToHashSet();

		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		var registrations = await AsyncExecuter.ToListAsync(registrationQueryable.Where(x =>
			x.TenantId == tenantId && x.AcademicYearId == academicYearId));
		var documentQueryable = await _documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQueryable.Where(x =>
			x.TenantId == tenantId && x.AcademicYearId == academicYearId));
		var checklistQueryable = await _checklistRepository.GetQueryableAsync();
		var checklists = await AsyncExecuter.ToListAsync(checklistQueryable.Where(x =>
			x.TenantId == tenantId && x.AcademicYearId == academicYearId));
		var examQueryable = await _examRepository.GetQueryableAsync();
		var exams = await AsyncExecuter.ToListAsync(examQueryable.Where(x =>
			x.TenantId == tenantId && x.AcademicYearId == academicYearId));

		var now = Clock.Now;
		var triggers = new Dictionary<AdmissionCrmJourneyTrigger, HashSet<Guid>>
		{
			[AdmissionCrmJourneyTrigger.RegistrationIssued] = registrations
				.Where(x => x.Status == AdmissionCrmRegistrationStatus.Issued)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationIncomplete] = registrations
				.Where(x => IsIncomplete(x.Status) && (now - (x.LastSavedAt ?? x.IssuedAt)).TotalDays >= 7)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationExpiring] = registrations
				.Where(x => IsIncomplete(x.Status) && x.LinkExpiresAt <= now.AddDays(3))
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationCorrectionRequested] = registrations
				.Where(x => x.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationSubmitted] = registrations
				.Where(x => x.Status is AdmissionCrmRegistrationStatus.Submitted or AdmissionCrmRegistrationStatus.Resubmitted)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.RegistrationVerified] = registrations
				.Where(x => x.Status is AdmissionCrmRegistrationStatus.Verified or AdmissionCrmRegistrationStatus.AdmissionReady)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentUploaded] = CurrentDocuments(documents)
				.Where(x => x.Status == AdmissionCrmApplicantDocumentStatus.Uploaded)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentRejected] = CurrentDocuments(documents)
				.Where(x => x.Status == AdmissionCrmApplicantDocumentStatus.Rejected)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.DocumentPending] = CurrentDocuments(documents)
				.Where(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview &&
				            (now - x.UploadedAt).TotalDays >= 2)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.ExamNoShow] = exams
				.Where(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ||
				            x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked && x.ExamEndTime.UtcDateTime < now)
				.Select(x => x.LeadId).ToHashSet(),
			[AdmissionCrmJourneyTrigger.AdmissionReady] = checklists
				.Where(x => x.ReadinessConfirmedAt.HasValue)
				.Select(x => x.LeadId).ToHashSet()
		};

		var result = new AdmissionCrmLifecycleAutomationResultDto
		{
			AcademicYearId = academicYearId,
			EvaluatedLeadCount = activeLeadIds.Count
		};
		var triggeredLeadIds = new HashSet<Guid>();
		var counts = new Dictionary<AdmissionCrmJourneyTrigger, int>();
		foreach (var (trigger, leadIds) in triggers)
		{
			foreach (var leadId in leadIds.Where(activeLeadIds.Contains))
			{
				var journey = await _leadAppService.RunJourneyRulesFromAutomationAsync(
					academicYearId,
					new RunAdmissionCrmJourneyRulesDto { Trigger = trigger, LeadId = leadId });
				result.CreatedTaskCount += journey.CreatedTaskCount;
				result.SentMessageCount += journey.SentMessageCount;
				triggeredLeadIds.Add(leadId);
				counts[trigger] = counts.GetValueOrDefault(trigger) + 1;
			}
		}
		result.TriggeredLeadCount = triggeredLeadIds.Count;
		result.TriggerCounts = counts;
		var expiry = await _finalAdmissionAppService.RunOfferExpiryFromAutomationAsync(academicYearId);
		result.ExpiredOfferCount = expiry.ExpiredCount;
		result.OfferExpiryFailureCount = expiry.FailedCount;
		return result;
	}

	private static bool IsIncomplete(AdmissionCrmRegistrationStatus status) =>
		status is AdmissionCrmRegistrationStatus.Issued
			or AdmissionCrmRegistrationStatus.Opened
			or AdmissionCrmRegistrationStatus.Started
			or AdmissionCrmRegistrationStatus.PartiallyCompleted
			or AdmissionCrmRegistrationStatus.ReturnedForCorrection;

	private static IEnumerable<AdmissionCrmApplicantDocument> CurrentDocuments(
		IEnumerable<AdmissionCrmApplicantDocument> documents) =>
		documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First());
}
