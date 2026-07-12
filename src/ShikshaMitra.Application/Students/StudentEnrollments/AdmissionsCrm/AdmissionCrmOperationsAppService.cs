using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmOperationsAppService : ApplicationService, IAdmissionCrmOperationsAppService
{
	private const int RegistrationExpiryWarningDays = 3;
	private const int RegistrationAbandonmentWarningDays = 7;
	private const int DocumentReviewWarningDays = 2;

	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _registrationRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _checklistRepository;
	private readonly IRepository<AdmissionCrmApplicantDocument, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _routeRepository;
	private readonly IRepository<AdmissionCrmRouteAppeal, Guid> _routeAppealRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmServiceHandoff, Guid> _serviceHandoffRepository;
	private readonly IRepository<AdmissionCrmPolicyException, Guid> _policyExceptionRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmApplicantIdentityProfile, Guid> _identityProfileRepository;
	private readonly IRepository<AdmissionCrmAdmissionDecision, Guid> _decisionRepository;
	private readonly IRepository<AdmissionCrmOperationsSavedView, Guid> _savedViewRepository;
	private readonly IRepository<AdmissionCrmWorkDelegation, Guid> _delegationRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmOperationsAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
		IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
		IRepository<AdmissionCrmRouteAppeal, Guid> routeAppealRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmServiceHandoff, Guid> serviceHandoffRepository,
		IRepository<AdmissionCrmPolicyException, Guid> policyExceptionRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityProfileRepository,
		IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
		IRepository<AdmissionCrmOperationsSavedView, Guid> savedViewRepository,
		IRepository<AdmissionCrmWorkDelegation, Guid> delegationRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_examRepository = examRepository;
		_slotRepository = slotRepository;
		_registrationRepository = registrationRepository;
		_checklistRepository = checklistRepository;
		_documentRepository = documentRepository;
		_routeRepository = routeRepository;
		_routeAppealRepository = routeAppealRepository;
		_activationRepository = activationRepository;
		_serviceHandoffRepository = serviceHandoffRepository;
		_policyExceptionRepository = policyExceptionRepository;
		_quotationRepository = quotationRepository;
		_identityProfileRepository = identityProfileRepository;
		_decisionRepository = decisionRepository;
		_savedViewRepository = savedViewRepository;
		_delegationRepository = delegationRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId)
	{
		var data = await LoadDataAsync(academicYearId, nameof(GetQueueSummaryAsync));
		var queues = Enum.GetValues<AdmissionCrmOperationsQueueType>()
			.Select(queueType =>
			{
				var items = BuildQueueItems(data, queueType);
				return new AdmissionCrmOperationsQueueCountDto
				{
					QueueType = queueType,
					Label = GetQueueLabel(queueType),
					Count = items.Count,
					UrgentCount = items.Count(x => x.IsUrgent)
				};
			})
			.ToList();

		return new AdmissionCrmOperationsQueueSummaryDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = Clock.Now,
			Queues = queues
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(
		Guid academicYearId,
		AdmissionCrmOperationsQueueType queueType,
		GetAdmissionCrmOperationsQueueInput input)
	{
		if (!Enum.IsDefined(queueType))
		{
			throw new BusinessException("AdmissionCrm:InvalidOperationsQueueType");
		}

		var data = await LoadDataAsync(academicYearId, nameof(GetQueueAsync));
		var items = BuildQueueItems(data, queueType).AsEnumerable();
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			items = items.Where(x => x.ApplicantName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				(x.Details?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (input.OwnerUserId.HasValue) items = items.Where(x => x.OwnerUserId == input.OwnerUserId);
		if (input.UrgentOnly == true) items = items.Where(x => x.IsUrgent);
		if (input.MinimumAgeDays.HasValue) items = items.Where(x => x.AgeDays >= input.MinimumAgeDays.Value);
		if (input.DueBefore.HasValue) items = items.Where(x => x.DueAt.HasValue && x.DueAt <= input.DueBefore.Value);
		if (input.AssignedToUserId.HasValue)
		{
			var delegatedOwners = await GetDelegatedOwnersAsync(academicYearId, queueType, input.AssignedToUserId.Value);
			items = items.Where(x => x.OwnerUserId == input.AssignedToUserId || (x.OwnerUserId.HasValue && delegatedOwners.Contains(x.OwnerUserId.Value)));
		}
		var filtered = items.ToList();
		return new AdmissionCrmOperationsQueueDto
		{
			AcademicYearId = academicYearId,
			QueueType = queueType,
			Label = GetQueueLabel(queueType),
			GeneratedAt = Clock.Now,
			TotalCount = filtered.Count,
			Items = filtered.Skip(input.SkipCount).Take(Math.Min(input.MaxResultCount, 200)).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public async Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, CreateAdmissionCrmOperationsSavedViewDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateSavedViewAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		var entity = new AdmissionCrmOperationsSavedView(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			userId, input.Name, input.QueueType, JsonSerializer.Serialize(input.Filters), input.IsShared);
		await _savedViewRepository.InsertAsync(entity, autoSave: true);
		return MapSavedView(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetSavedViewsAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		var queryable = await _savedViewRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (x.IsShared || x.OwnerUserId == userId)));
		return items.OrderBy(x => x.Name).Select(MapSavedView).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public async Task DeleteSavedViewAsync(Guid id)
	{
		var entity = await _savedViewRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(DeleteSavedViewAsync));
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		if (entity.OwnerUserId != userId && !entity.IsShared) throw new AbpAuthorizationException("Only the saved-view owner can delete this view.");
		await _savedViewRepository.DeleteAsync(entity, autoSave: true);
	}

	private async Task<HashSet<Guid>> GetDelegatedOwnersAsync(Guid academicYearId, AdmissionCrmOperationsQueueType queueType, Guid delegateUserId)
	{
		var now = Clock.Now;
		var queryable = await _delegationRepository.GetQueryableAsync();
		var delegations = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.DelegateUserId == delegateUserId &&
			x.Status != AdmissionCrmDelegationStatus.Cancelled && x.StartsAt <= now && x.EndsAt >= now));
		return delegations.Where(x =>
			(JsonSerializer.Deserialize<List<AdmissionCrmOperationsQueueType>>(x.QueueTypesJson) ?? []).Contains(queueType))
			.Select(x => x.DelegatorUserId).ToHashSet();
	}

	private static AdmissionCrmOperationsSavedViewDto MapSavedView(AdmissionCrmOperationsSavedView entity) => new()
	{
		Id = entity.Id, AcademicYearId = entity.AcademicYearId, OwnerUserId = entity.OwnerUserId,
		Name = entity.Name, QueueType = entity.QueueType, IsShared = entity.IsShared,
		Filters = JsonSerializer.Deserialize<GetAdmissionCrmOperationsQueueInput>(entity.FiltersJson) ?? new GetAdmissionCrmOperationsQueueInput()
	};

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public async Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(
		Guid registrationApplicationId)
	{
		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		var registration = await AsyncExecuter.FirstOrDefaultAsync(registrationQueryable.Where(x =>
			x.Id == registrationApplicationId && x.TenantId == CurrentTenant.Id));
		if (registration == null)
		{
			throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			registration.AcademicYearId,
			nameof(GetAdmissionHandoffPackageAsync));
		if (registration.Status != AdmissionCrmRegistrationStatus.AdmissionReady || !registration.VerifiedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:RegistrationNotAdmissionReady");
		}

		var checklistQueryable = await _checklistRepository.GetQueryableAsync();
		var checklist = await AsyncExecuter.FirstOrDefaultAsync(checklistQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.RegistrationApplicationId == registration.Id));
		if (checklist?.ReadinessConfirmedAt == null)
		{
			throw new BusinessException("AdmissionCrm:ReadinessNotConfirmed");
		}

		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var lead = await AsyncExecuter.FirstOrDefaultAsync(leadQueryable.Where(x =>
			x.Id == registration.LeadId && x.TenantId == CurrentTenant.Id));
		if (lead == null)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound");
		}

		var examQueryable = await _examRepository.GetQueryableAsync();
		var exam = await AsyncExecuter.FirstOrDefaultAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var documentQueryable = await _documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == checklist.Id));
		var currentDocuments = documents
			.Where(x => x.SupersededAt == null)
			.OrderBy(x => x.RequirementKey)
			.ThenByDescending(x => x.Version)
			.ToList();

		return new AdmissionCrmAdmissionHandoffPackageDto
		{
			AcademicYearId = registration.AcademicYearId,
			LeadId = lead.Id,
			RegistrationApplicationId = registration.Id,
			ChecklistAssignmentId = checklist.Id,
			ApplicantName = GetApplicantName(lead),
			FormNumber = registration.FormNumber,
			LeadStage = lead.Stage,
			RegistrationStatus = registration.Status,
			RegistrationVerifiedAt = registration.VerifiedAt.Value,
			ReadinessConfirmedAt = checklist.ReadinessConfirmedAt.Value,
			ReadinessOverridden = checklist.ReadinessOverridden,
			ReadinessReason = checklist.ReadinessReason,
			EligibilityDecision = exam?.EligibilityDecision,
			MeritRank = exam?.MeritRank,
			PaymentReference = registration.PaymentReference,
			FeeStatus = registration.FeeStatus,
			ApplicantProfile = new Dictionary<string, string?>
			{
				["firstName"] = lead.FirstName,
				["middleName"] = lead.MiddleName,
				["lastName"] = lead.LastName,
				["dateOfBirth"] = lead.DateOfBirth?.ToString("yyyy-MM-dd"),
				["parentGuardianName"] = lead.ParentGuardianName,
				["primaryContactNo"] = lead.PrimaryContactNo,
				["email"] = lead.Email,
				["desiredClassName"] = lead.DesiredClassName,
				["campus"] = lead.CampusBranchName
			},
			Documents = currentDocuments.Select(x => new AdmissionCrmAdmissionHandoffDocumentDto
			{
				RequirementKey = x.RequirementKey,
				Version = x.Version,
				Status = x.Status,
				FileName = x.FileName,
				ValidUntil = x.ValidUntil,
				DecisionAt = x.DecisionAt,
				OriginalSeenAt = x.OriginalSeenAt
			}).ToList(),
			GeneratedAt = Clock.Now
		};
	}

	private async Task<OperationsData> LoadDataAsync(Guid academicYearId, string operation)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		var tenantId = CurrentTenant.Id;
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var examQueryable = await _examRepository.GetQueryableAsync();
		var slotQueryable = await _slotRepository.GetQueryableAsync();
		var registrationQueryable = await _registrationRepository.GetQueryableAsync();
		var checklistQueryable = await _checklistRepository.GetQueryableAsync();
		var documentQueryable = await _documentRepository.GetQueryableAsync();
		var routeQueryable = await _routeRepository.GetQueryableAsync();
		var routeAppealQueryable = await _routeAppealRepository.GetQueryableAsync();
		var activationQueryable = await _activationRepository.GetQueryableAsync();
		var serviceHandoffQueryable = await _serviceHandoffRepository.GetQueryableAsync();
		var policyExceptionQueryable = await _policyExceptionRepository.GetQueryableAsync();
		var quotationQueryable = await _quotationRepository.GetQueryableAsync();
		var identityProfileQueryable = await _identityProfileRepository.GetQueryableAsync();
		var decisionQueryable = await _decisionRepository.GetQueryableAsync();

		return new OperationsData(
			await AsyncExecuter.ToListAsync(leadQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(examQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(slotQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(registrationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(checklistQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(documentQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(routeQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(routeAppealQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(activationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(serviceHandoffQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(policyExceptionQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(quotationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(identityProfileQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(decisionQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			Clock.Now);
	}

	private static IReadOnlyList<AdmissionCrmOperationsQueueItemDto> BuildQueueItems(
		OperationsData data,
		AdmissionCrmOperationsQueueType queueType)
	{
		var leads = data.Leads.ToDictionary(x => x.Id);
		var items = queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => BuildUnassignedExamQueue(data, leads),
			AdmissionCrmOperationsQueueType.CapacityConflict => BuildCapacityConflictQueue(data, leads),
			AdmissionCrmOperationsQueueType.NoShow => BuildNoShowQueue(data, leads),
			AdmissionCrmOperationsQueueType.ExpiringRegistrationLink => BuildExpiringLinkQueue(data, leads),
			AdmissionCrmOperationsQueueType.IncompleteRegistration => BuildIncompleteRegistrationQueue(data, leads),
			AdmissionCrmOperationsQueueType.RejectedDocument => BuildDocumentQueue(data, leads, AdmissionCrmApplicantDocumentStatus.Rejected),
			AdmissionCrmOperationsQueueType.DocumentVerificationBacklog => BuildDocumentReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.AdmissionReady => BuildAdmissionReadyQueue(data, leads),
			AdmissionCrmOperationsQueueType.PolicyException => BuildPolicyExceptionQueue(data, leads),
			AdmissionCrmOperationsQueueType.PaymentCondition => BuildPaymentConditionQueue(data, leads),
			AdmissionCrmOperationsQueueType.HouseholdResolution => BuildHouseholdResolutionQueue(data, leads),
			AdmissionCrmOperationsQueueType.IdentityConflict => BuildIdentityConflictQueue(data, leads),
			AdmissionCrmOperationsQueueType.FinalApproval => BuildFinalApprovalQueue(data, leads),
			AdmissionCrmOperationsQueueType.RouteEligibilityReview => BuildRouteQueue(data, leads),
			AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation => BuildRteQueue(data, leads),
			AdmissionCrmOperationsQueueType.TransferEvidencePending => BuildTransferQueue(data, leads),
			AdmissionCrmOperationsQueueType.ActivationRepair => BuildActivationRepairQueue(data, leads),
			AdmissionCrmOperationsQueueType.ReturningIdentityMatch => BuildReturningIdentityMatchQueue(data, leads),
			AdmissionCrmOperationsQueueType.CancellationReview => BuildCancellationReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.RouteAppealReview => BuildRouteAppealReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.ServiceHandoffException => BuildServiceHandoffExceptionQueue(data, leads),
			_ => []
		};

		return items
			.OrderByDescending(x => x.IsUrgent)
			.ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
			.ThenByDescending(x => x.AgeDays)
			.ThenBy(x => x.ApplicantName)
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildUnassignedExamQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var scheduledLeadIds = data.Exams.Select(x => x.LeadId).ToHashSet();
		return data.Leads
			.Where(x => x.Stage == AdmissionInquiryStage.ExamReady && !scheduledLeadIds.Contains(x.Id))
			.Select(lead => CreateItem(lead, $"exam:{lead.Id}", "Entrance exam needs assignment", "No exam slot has been assigned.", "Exam ready", null, AgeDays(lead.LastActivityTime, data.Now), true))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildCapacityConflictQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var slots = data.Slots.ToDictionary(x => x.Id);
		var results = new List<AdmissionCrmOperationsQueueItemDto>();
		foreach (var group in data.Exams.GroupBy(x => x.EntranceExamSlotId))
		{
			if (!slots.TryGetValue(group.Key, out var slot) || group.Count() <= slot.StudentLimit)
			{
				continue;
			}

			var exam = group.OrderBy(x => x.ScheduledAt).First();
			if (!leads.TryGetValue(exam.LeadId, out var lead))
			{
				continue;
			}

			results.Add(CreateItem(
				lead,
				$"capacity:{slot.Id}",
				$"{slot.SlotName ?? "Entrance exam slot"} is over capacity",
				$"{group.Count()} assignments for {slot.StudentLimit} seats.",
				"Capacity conflict",
				slot.StartDate,
				AgeDays(exam.ScheduledAt, data.Now),
				true,
				examRecordId: exam.Id));
		}
		return results;
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildNoShowQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Exams
			.Where(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ||
			            (x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked && x.ExamEndTime.UtcDateTime < data.Now))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(exam => CreateItem(
				leads[exam.LeadId],
				$"no-show:{exam.Id}",
				exam.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ? "Entrance exam no-show" : "Attendance is overdue",
				$"{exam.SlotNameSnapshot ?? "Exam"} was scheduled for {exam.ExamStartTime:dd MMM yyyy, h:mm tt}.",
				exam.AttendanceStatus.ToString(),
				exam.ExamEndTime,
				AgeDays(exam.ExamEndTime.UtcDateTime, data.Now),
				true,
				examRecordId: exam.Id))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildExpiringLinkQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var warningAt = data.Now.AddDays(RegistrationExpiryWarningDays);
		return data.Registrations
			.Where(x => IsRegistrationOpen(x.Status) && x.LinkExpiresAt <= warningAt)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application => CreateItem(
				leads[application.LeadId],
				$"expiry:{application.Id}",
				application.LinkExpiresAt <= data.Now ? "Registration link expired" : "Registration link expiring",
				$"Form {application.FormNumber} expires {application.LinkExpiresAt:dd MMM yyyy, h:mm tt}.",
				application.Status.ToString(),
				ToOffset(application.LinkExpiresAt),
				AgeDays(application.IssuedAt, data.Now),
				application.LinkExpiresAt <= data.Now.AddDays(1),
				registrationApplicationId: application.Id))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildIncompleteRegistrationQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Registrations
			.Where(x => IsRegistrationOpen(x.Status))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application => CreateItem(
				leads[application.LeadId],
				$"incomplete:{application.Id}",
				"Registration is incomplete",
				$"Form {application.FormNumber} is {application.CompletionPercentage:0}% complete.",
				application.Status.ToString(),
				ToOffset(application.LinkExpiresAt),
				AgeDays(application.LastSavedAt ?? application.IssuedAt, data.Now),
				AgeDays(application.LastSavedAt ?? application.IssuedAt, data.Now) >= RegistrationAbandonmentWarningDays,
				registrationApplicationId: application.Id))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildDocumentQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		AdmissionCrmApplicantDocumentStatus status)
	{
		var registrationsByAssignment = data.Checklists.ToDictionary(x => x.Id, x => x.RegistrationApplicationId);
		return GetCurrentDocuments(data.Documents)
			.Where(x => x.Status == status && leads.ContainsKey(x.LeadId))
			.Select(document => CreateItem(
				leads[document.LeadId],
				$"document:{document.Id}",
				$"{document.RequirementKey} requires correction",
				document.DecisionReason,
				document.Status.ToString(),
				null,
				AgeDays(document.DecisionAt ?? document.UploadedAt, data.Now),
				AgeDays(document.DecisionAt ?? document.UploadedAt, data.Now) >= DocumentReviewWarningDays,
				registrationApplicationId: registrationsByAssignment.GetValueOrDefault(document.ChecklistAssignmentId),
				documentId: document.Id,
				checklistAssignmentId: document.ChecklistAssignmentId))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildDocumentReviewQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var registrationsByAssignment = data.Checklists.ToDictionary(x => x.Id, x => x.RegistrationApplicationId);
		return GetCurrentDocuments(data.Documents)
			.Where(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(document => CreateItem(
				leads[document.LeadId],
				$"review:{document.Id}",
				$"Review {document.RequirementKey}",
				document.FileName,
				document.Status.ToString(),
				null,
				AgeDays(document.UploadedAt, data.Now),
				AgeDays(document.UploadedAt, data.Now) >= DocumentReviewWarningDays,
				registrationApplicationId: registrationsByAssignment.GetValueOrDefault(document.ChecklistAssignmentId),
				documentId: document.Id,
				checklistAssignmentId: document.ChecklistAssignmentId))
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildAdmissionReadyQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var registrations = data.Registrations.ToDictionary(x => x.Id);
		return data.Checklists
			.Where(x => x.ReadinessConfirmedAt.HasValue && registrations.TryGetValue(x.RegistrationApplicationId, out var registration) && registration.Status == AdmissionCrmRegistrationStatus.AdmissionReady)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(checklist =>
			{
				var registration = registrations[checklist.RegistrationApplicationId];
				return CreateItem(
					leads[checklist.LeadId],
					$"ready:{registration.Id}",
					"Admission handoff is ready",
					$"Form {registration.FormNumber} passed registration and document verification.",
					registration.Status.ToString(),
					null,
					AgeDays(checklist.ReadinessConfirmedAt!.Value, data.Now),
					false,
					registrationApplicationId: registration.Id,
					checklistAssignmentId: checklist.Id);
			})
			.ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildRouteQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.Status is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"route:{x.Id}",
				"Admission route requires review", x.EvaluationReason ?? x.AssignmentReason, x.Status.ToString(), null,
				AgeDays(x.AssignedAt, data.Now), x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildRteQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType == AdmissionCrmRouteType.RteEws && x.Status is not (AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved))
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"rte:{x.Id}",
				"Government allotment needs reconciliation", x.AssignmentReason, x.Status.ToString(), null, AgeDays(x.AssignedAt, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildTransferQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType is AdmissionCrmRouteType.Transfer or AdmissionCrmRouteType.Lateral or AdmissionCrmRouteType.Interstate && x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"transfer:{x.Id}",
				"Transfer evidence is pending", x.EvaluationReason, x.Status.ToString(), null, AgeDays(x.AssignedAt, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildActivationRepairQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Activations.Where(x => x.Status == AdmissionCrmActivationStatus.RepairRequired).Where(x => leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"activation:{x.Id}", "Activation repair required", x.RepairReason,
				x.RepairStep ?? x.Status.ToString(), null, AgeDays(x.LastModificationTime ?? x.RequestedAt, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildCancellationReviewQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Activations.Where(x => x.Status == AdmissionCrmActivationStatus.CancellationPending).Where(x => leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"activation-cancellation:{x.Id}", "Activation cancellation requires approval",
				x.CancellationReason, x.Status.ToString(), null, AgeDays(x.CancellationRequestedAt ?? x.RequestedAt, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildRouteAppealReviewQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.RouteAppeals.Where(x => x.Status is AdmissionCrmRouteAppealStatus.Requested or AdmissionCrmRouteAppealStatus.UnderReview)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"route-appeal:{x.Id}",
				"Admission route appeal requires review", x.Reason, x.RequestedStatus.ToString(), null,
				AgeDays(x.RequestedAt, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildServiceHandoffExceptionQueue(
		OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.ServiceHandoffs.Where(x => x.Status is AdmissionCrmServiceHandoffStatus.Pending or AdmissionCrmServiceHandoffStatus.Failed)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"service-handoff:{x.Id}",
				$"{x.Type} service handoff needs attention", x.LastError ?? $"Dispatch to {x.DestinationModule} is pending.",
				x.Status.ToString(), null, AgeDays(x.RequestedAt, data.Now), x.Status == AdmissionCrmServiceHandoffStatus.Failed,
				serviceHandoffId: x.Id)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildPolicyExceptionQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.PolicyExceptions.Where(x => x.Status == AdmissionCrmPolicyExceptionStatus.ApprovalPending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"policy-exception:{x.Id}",
				"Policy exception requires approval", x.Reason, x.Status.ToString(), x.ExpiresAt.HasValue ? ToOffset(x.ExpiresAt.Value) : null,
				AgeDays(x.RequestedAt, data.Now), !x.ExpiresAt.HasValue || x.ExpiresAt.Value <= data.Now.AddDays(2))).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildPaymentConditionQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Quotations.Where(x => x.Status == AdmissionCrmQuotationStatus.Issued && !x.IsGateSatisfiedAt(data.Now))
			.GroupBy(x => x.LeadId).Select(x => x.OrderByDescending(q => q.Version).First())
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"payment:{x.Id}",
				"Admission payment condition is pending", $"{x.Currency} {Math.Max(0, x.RequiredAmount - x.ConfirmedAmount):0.00} remains against {x.QuotationNumber}.",
				x.FinanceState.ToString(), ToOffset(x.ValidUntil), AgeDays(x.IssuedAt ?? x.CreationTime, data.Now), x.ValidUntil <= data.Now.AddDays(2))).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildHouseholdResolutionQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var profiles = data.IdentityProfiles.ToDictionary(x => x.LeadId);
		return data.Leads.Where(x => !profiles.TryGetValue(x.Id, out var profile) ||
			profile.Status is AdmissionCrmIdentityResolutionStatus.Unresolved or AdmissionCrmIdentityResolutionStatus.PendingReview)
			.Select(x => CreateItem(x, $"household:{x.Id}", "Household and guardian resolution is pending",
				profiles.GetValueOrDefault(x.Id)?.ConflictReason, profiles.GetValueOrDefault(x.Id)?.Status.ToString() ?? "NotStarted",
				null, AgeDays(x.LastActivityTime, data.Now), x.Stage >= AdmissionInquiryStage.Eligible)).ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildIdentityConflictQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.IdentityProfiles.Where(x => x.Status == AdmissionCrmIdentityResolutionStatus.Conflict && leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"identity-conflict:{x.Id}", "Identity conflict requires resolution",
				x.ConflictReason, x.Status.ToString(), null, AgeDays(x.LastModificationTime ?? x.CreationTime, data.Now), true)).ToList();

	private static List<AdmissionCrmOperationsQueueItemDto> BuildFinalApprovalQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var decidedLeadIds = data.Decisions.Where(x => x.Status != AdmissionCrmAdmissionDecisionStatus.Pending).Select(x => x.LeadId).ToHashSet();
		return data.Registrations.Where(x => x.Status == AdmissionCrmRegistrationStatus.AdmissionReady && !decidedLeadIds.Contains(x.LeadId))
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"final-approval:{x.Id}",
				"Final admission approval is pending", $"Form {x.FormNumber} is admission ready.", "Pending",
				null, AgeDays(x.VerifiedAt ?? x.LastModificationTime ?? x.IssuedAt, data.Now), true, registrationApplicationId: x.Id)).ToList();
	}

	private static List<AdmissionCrmOperationsQueueItemDto> BuildReturningIdentityMatchQueue(OperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType == AdmissionCrmRouteType.ReturningStudent &&
			x.Status is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"returning:{x.Id}",
				"Former student identity match requires review", x.EvaluationReason ?? x.AssignmentReason, x.Status.ToString(),
				null, AgeDays(x.AssignedAt, data.Now), true)).ToList();

	private static IEnumerable<AdmissionCrmApplicantDocument> GetCurrentDocuments(
		IEnumerable<AdmissionCrmApplicantDocument> documents)
	{
		return documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First());
	}

	private static AdmissionCrmOperationsQueueItemDto CreateItem(
		AdmissionInquiryLead lead,
		string key,
		string title,
		string? details,
		string status,
		DateTimeOffset? dueAt,
		int ageDays,
		bool urgent,
		Guid? examRecordId = null,
		Guid? registrationApplicationId = null,
		Guid? documentId = null,
		Guid? checklistAssignmentId = null,
		Guid? serviceHandoffId = null)
	{
		return new AdmissionCrmOperationsQueueItemDto
		{
			Key = key,
			LeadId = lead.Id,
			ExamRecordId = examRecordId,
			RegistrationApplicationId = registrationApplicationId,
			DocumentId = documentId,
			ChecklistAssignmentId = checklistAssignmentId,
			ServiceHandoffId = serviceHandoffId,
			ApplicantName = GetApplicantName(lead),
			Title = title,
			Details = details,
			Status = status,
			DueAt = dueAt,
			AgeDays = ageDays,
			IsUrgent = urgent,
			OwnerUserId = lead.OwnerUserId
		};
	}

	private static bool IsRegistrationOpen(AdmissionCrmRegistrationStatus status)
	{
		return status is AdmissionCrmRegistrationStatus.Issued
			or AdmissionCrmRegistrationStatus.Opened
			or AdmissionCrmRegistrationStatus.Started
			or AdmissionCrmRegistrationStatus.PartiallyCompleted
			or AdmissionCrmRegistrationStatus.ReturnedForCorrection
			or AdmissionCrmRegistrationStatus.Expired;
	}

	private static int AgeDays(DateTime value, DateTime now) => Math.Max(0, (now.Date - value.Date).Days);
	private static int AgeDays(DateTimeOffset value, DateTime now) => AgeDays(value.UtcDateTime, now);
	private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

	private static string GetApplicantName(AdmissionInquiryLead lead)
	{
		var name = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(name) ? lead.ParentGuardianName ?? lead.PrimaryContactNo ?? "Applicant" : name;
	}

	private static string GetQueueLabel(AdmissionCrmOperationsQueueType queueType)
	{
		return queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => "Unassigned exams",
			AdmissionCrmOperationsQueueType.CapacityConflict => "Capacity conflicts",
			AdmissionCrmOperationsQueueType.NoShow => "No-shows and overdue attendance",
			AdmissionCrmOperationsQueueType.ExpiringRegistrationLink => "Expiring registration links",
			AdmissionCrmOperationsQueueType.IncompleteRegistration => "Incomplete registrations",
			AdmissionCrmOperationsQueueType.RejectedDocument => "Rejected documents",
			AdmissionCrmOperationsQueueType.DocumentVerificationBacklog => "Document verification backlog",
			AdmissionCrmOperationsQueueType.AdmissionReady => "Admission-ready applicants",
			AdmissionCrmOperationsQueueType.PolicyException => "Policy exceptions",
			AdmissionCrmOperationsQueueType.PaymentCondition => "Payment conditions",
			AdmissionCrmOperationsQueueType.HouseholdResolution => "Household resolution",
			AdmissionCrmOperationsQueueType.IdentityConflict => "Identity conflicts",
			AdmissionCrmOperationsQueueType.FinalApproval => "Final admission approvals",
			AdmissionCrmOperationsQueueType.RouteEligibilityReview => "Route eligibility review",
			AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation => "Government allotment reconciliation",
			AdmissionCrmOperationsQueueType.TransferEvidencePending => "Transfer evidence pending",
			AdmissionCrmOperationsQueueType.ActivationRepair => "Activation repair",
			AdmissionCrmOperationsQueueType.ReturningIdentityMatch => "Returning-student identity matches",
			AdmissionCrmOperationsQueueType.CancellationReview => "Activation cancellation review",
			AdmissionCrmOperationsQueueType.RouteAppealReview => "Admission route appeal review",
			AdmissionCrmOperationsQueueType.ServiceHandoffException => "Optional service handoff exceptions",
			_ => queueType.ToString()
		};
	}

	private sealed record OperationsData(
		IReadOnlyList<AdmissionInquiryLead> Leads,
		IReadOnlyList<AdmissionCrmEntranceExamRecord> Exams,
		IReadOnlyList<AdmissionCrmExamSlot> Slots,
		IReadOnlyList<AdmissionCrmRegistrationApplication> Registrations,
		IReadOnlyList<AdmissionCrmDocumentChecklistAssignment> Checklists,
		IReadOnlyList<AdmissionCrmApplicantDocument> Documents,
		IReadOnlyList<AdmissionCrmRouteAssignment> Routes,
		IReadOnlyList<AdmissionCrmRouteAppeal> RouteAppeals,
		IReadOnlyList<AdmissionCrmActivation> Activations,
		IReadOnlyList<AdmissionCrmServiceHandoff> ServiceHandoffs,
		IReadOnlyList<AdmissionCrmPolicyException> PolicyExceptions,
		IReadOnlyList<AdmissionCrmAdmissionQuotation> Quotations,
		IReadOnlyList<AdmissionCrmApplicantIdentityProfile> IdentityProfiles,
		IReadOnlyList<AdmissionCrmAdmissionDecision> Decisions,
		DateTime Now);
}
