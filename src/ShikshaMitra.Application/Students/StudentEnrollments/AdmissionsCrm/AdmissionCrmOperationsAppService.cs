using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

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
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmOperationsSavedView, Guid> _savedViewRepository;
	private readonly IRepository<AdmissionCrmWorkDelegation, Guid> _delegationRepository;
	private readonly IRepository<AdmissionCrmFollowUpTask, Guid> _taskRepository;
	private readonly IRepository<IdentityUser, Guid> _userRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<EducationMediumStage, Guid> _mediumStageRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly AdmissionCrmApplicantProgressReadService _progressReadService;

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
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmOperationsSavedView, Guid> savedViewRepository,
		IRepository<AdmissionCrmWorkDelegation, Guid> delegationRepository,
		IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
		IRepository<IdentityUser, Guid> userRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<EducationMediumStage, Guid> mediumStageRepository,
		StudentScopeAccessService studentScopeAccessService,
		AdmissionCrmApplicantProgressReadService progressReadService)
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
		_offerRepository = offerRepository;
		_seatPoolRepository = seatPoolRepository;
		_savedViewRepository = savedViewRepository;
		_delegationRepository = delegationRepository;
		_taskRepository = taskRepository;
		_userRepository = userRepository;
		_standardRepository = standardRepository;
		_mediumStageRepository = mediumStageRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_progressReadService = progressReadService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmMyWorkDto> GetMyWorkAsync(
		Guid academicYearId,
		GetAdmissionCrmMyWorkInput input)
	{
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetMyWorkAsync));
		var now = Clock.Now;
		var today = now.ToUniversalTime().Date;
		var tomorrow = today.AddDays(1);
		var dueSoonEnd = today.AddDays(4);
		var tenantId = CurrentTenant.Id;
		var currentUser = await _userRepository.GetAsync(userId);
		var currentUserDisplayName = GetUserDisplayName(currentUser);
		var leadQueryable = await _leadRepository.GetQueryableAsync();
		var scopedLeads = leadQueryable.Where(lead =>
			lead.TenantId == tenantId &&
			lead.AcademicYearId == academicYearId);
		scopedLeads = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeads);
		var taskQueryable = await _taskRepository.GetQueryableAsync();
		var assignedTasks = taskQueryable.Where(task =>
			task.TenantId == tenantId &&
			task.AcademicYearId == academicYearId &&
			task.Status == AdmissionCrmTaskStatus.Pending &&
			task.AssignedUserId == userId &&
			scopedLeads.Any(lead => lead.Id == task.LeadId));

		var assignedTotal = await AsyncExecuter.CountAsync(assignedTasks);
		var overdueCount = await AsyncExecuter.CountAsync(assignedTasks.Where(task => task.DueAt < today));
		var dueTodayCount = await AsyncExecuter.CountAsync(assignedTasks.Where(task =>
			task.DueAt >= today &&
			task.DueAt < tomorrow));

		IQueryable<AdmissionCrmFollowUpTask> filtered = assignedTasks;
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			var applicantNumber = ParseApplicantNumber(input.Search);
			filtered = filtered.Where(task =>
				task.Title.ToLower().Contains(search) ||
				(task.Notes != null && task.Notes.ToLower().Contains(search)) ||
				scopedLeads.Any(lead =>
					lead.Id == task.LeadId &&
					((applicantNumber.HasValue && lead.ApplicantNumber == applicantNumber.Value) ||
					 (lead.FirstName != null && lead.FirstName.ToLower().Contains(search)) ||
					 (lead.MiddleName != null && lead.MiddleName.ToLower().Contains(search)) ||
					 (lead.LastName != null && lead.LastName.ToLower().Contains(search)) ||
					 (lead.ParentGuardianName != null && lead.ParentGuardianName.ToLower().Contains(search)) ||
					 (lead.PrimaryContactNo != null && lead.PrimaryContactNo.Contains(search)) ||
					 (lead.Email != null && lead.Email.ToLower().Contains(search)))));
		}
		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var search = input.ApplicantSearch.Trim().ToLowerInvariant();
			var applicantNumber = ParseApplicantNumber(input.ApplicantSearch);
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				((applicantNumber.HasValue && lead.ApplicantNumber == applicantNumber.Value) ||
				 (lead.FirstName != null && lead.FirstName.ToLower().Contains(search)) ||
				 (lead.MiddleName != null && lead.MiddleName.ToLower().Contains(search)) ||
				 (lead.LastName != null && lead.LastName.ToLower().Contains(search)))));
		}
		if (!string.IsNullOrWhiteSpace(input.TargetClassSearch))
		{
			var search = input.TargetClassSearch.Trim().ToLowerInvariant();
			var standardQueryable = await _standardRepository.GetQueryableAsync();
			var matchingStandardIds = await AsyncExecuter.ToListAsync(standardQueryable
				.Where(standard => standard.Name.ToLower().Contains(search))
				.Select(standard => standard.Id));
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				((lead.DesiredClassName != null && lead.DesiredClassName.ToLower().Contains(search)) ||
				 (lead.TargetStandardId.HasValue &&
				  matchingStandardIds.Contains(lead.TargetStandardId.Value)))));
		}
		if (!string.IsNullOrWhiteSpace(input.AssignedSearch) &&
			!currentUserDisplayName.Contains(input.AssignedSearch.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.WorkType.HasValue && input.WorkType != AdmissionCrmMyWorkType.Task)
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.QueueType.HasValue)
		{
			filtered = filtered.Where(_ => false);
		}
		if (input.Stage.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.Stage == input.Stage.Value));
		}
		if (input.TargetStandardId.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.TargetStandardId == input.TargetStandardId.Value));
		}
		if (input.AcademicMediumId.HasValue)
		{
			await _studentScopeAccessService.EnsureCanAccessMediumAsync(
				input.AcademicMediumId.Value,
				nameof(GetMyWorkAsync));
			var stageQueryable = await _mediumStageRepository.GetQueryableAsync();
			var stageIds = await AsyncExecuter.ToListAsync(stageQueryable
				.Where(x => x.EducationMediumId == input.AcademicMediumId.Value)
				.Select(x => x.Id));
			var standardQueryable = await _standardRepository.GetQueryableAsync();
			var standardIds = await AsyncExecuter.ToListAsync(standardQueryable
				.Where(x => stageIds.Contains(x.EducationMediumStageId))
				.Select(x => x.Id));
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				(!lead.TargetStandardId.HasValue ||
				 standardIds.Contains(lead.TargetStandardId.Value))));
		}
		if (input.Priority.HasValue)
		{
			filtered = filtered.Where(task => scopedLeads.Any(lead =>
				lead.Id == task.LeadId &&
				lead.Priority == input.Priority.Value));
		}
		if (input.Urgency.HasValue)
		{
			filtered = input.Urgency.Value switch
			{
				AdmissionCrmWorkUrgency.Overdue =>
					filtered.Where(task => task.DueAt < today),
				AdmissionCrmWorkUrgency.DueToday =>
					filtered.Where(task => task.DueAt >= today && task.DueAt < tomorrow),
				AdmissionCrmWorkUrgency.DueSoon =>
					filtered.Where(task => task.DueAt >= tomorrow && task.DueAt < dueSoonEnd),
				_ =>
					filtered.Where(task => task.DueAt >= dueSoonEnd)
			};
		}

		var descending = input.Sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var sortField = input.Sorting?
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()?
			.ToLowerInvariant();
		filtered = sortField switch
		{
			"applicantnumber" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.ApplicantNumber)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.ApplicantNumber)
					.FirstOrDefault()),
			"applicantname" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.FirstName)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.FirstName)
					.FirstOrDefault()),
			"priority" => descending
				? filtered.OrderByDescending(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.Priority)
					.FirstOrDefault())
				: filtered.OrderBy(task => scopedLeads
					.Where(lead => lead.Id == task.LeadId)
					.Select(lead => lead.Priority)
					.FirstOrDefault()),
			"title" => descending
				? filtered.OrderByDescending(task => task.Title)
				: filtered.OrderBy(task => task.Title),
			"dueat" => descending
				? filtered.OrderByDescending(task => task.DueAt)
				: filtered.OrderBy(task => task.DueAt),
			_ => filtered.OrderBy(task => task.DueAt)
				.ThenBy(task => task.CreationTime)
		};
		var totalCount = await AsyncExecuter.CountAsync(filtered);
		var tasks = await AsyncExecuter.ToListAsync(filtered
			.Skip(Math.Max(0, input.SkipCount))
			.Take(Math.Clamp(input.MaxResultCount, 1, 200)));
		// Keep this as a List so EF translates Enumerable.Contains. With newer runtimes,
		// a captured Guid[] can bind to the ReadOnlySpan overload and fail during query evaluation.
		var leadIds = tasks.Select(task => task.LeadId).Distinct().ToList();
		var leads = leadIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync(scopedLeads.Where(lead => leadIds.Contains(lead.Id)));
		var leadById = leads.ToDictionary(lead => lead.Id);
		var progressByLead = await _progressReadService.GetForLeadsAsync(leads);
		var targetStandardIds = leads
			.Where(lead => lead.TargetStandardId.HasValue)
			.Select(lead => lead.TargetStandardId!.Value)
			.Distinct()
			.ToList();
		var standards = targetStandardIds.Count == 0
			? []
			: await _standardRepository.GetListAsync(standard => targetStandardIds.Contains(standard.Id));
		var standardNames = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
		var ownerUserIds = leads
			.Where(lead => lead.OwnerUserId.HasValue)
			.Select(lead => lead.OwnerUserId!.Value)
			.Distinct()
			.ToList();
		var owners = ownerUserIds.Count == 0
			? []
			: await _userRepository.GetListAsync(user => ownerUserIds.Contains(user.Id));
		var ownerNames = owners.ToDictionary(user => user.Id, GetUserDisplayName);
		var workItems = new List<AdmissionCrmMyWorkItemDto>(tasks.Count);
		foreach (var task in tasks)
		{
			if (!leadById.TryGetValue(task.LeadId, out var lead))
			{
				continue;
			}

			var item = CreateTaskWorkItem(task, lead, userId, now);
			item.AssignedDisplayName = currentUserDisplayName;
			item.OwnerDisplayName = lead.OwnerUserId.HasValue
				? ownerNames.GetValueOrDefault(lead.OwnerUserId.Value)
				: null;
			item.TargetClassName = lead.DesiredClassName ??
				(lead.TargetStandardId.HasValue
					? standardNames.GetValueOrDefault(lead.TargetStandardId.Value)
					: null);
			if (progressByLead.TryGetValue(lead.Id, out var progress))
			{
				item.PipelineColumn = progress.PipelineColumn;
				item.CurrentStep = progress.CurrentStep;
				item.NextAction = progress.NextAction;
				item.NextActionCode = progress.NextActionCode;
			}

			workItems.Add(item);
		}

		return new AdmissionCrmMyWorkDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = now,
			AssignedTotal = assignedTotal,
			OverdueCount = overdueCount,
			DueTodayCount = dueTodayCount,
			UrgentBlockerCount = 0,
			TotalCount = totalCount,
			Items = workItems
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId)
	{
		var data = await LoadDataAsync(academicYearId, nameof(GetOverviewAsync));
		var permittedQueueTypes = await GetPermittedQueueTypesAsync();
		var queueItems = permittedQueueTypes.ToDictionary(x => x, x => BuildQueueItems(data, x));
		var queueCounts = queueItems.Select(x => new AdmissionCrmOperationsQueueCountDto
		{
			QueueType = x.Key,
			Label = GetQueueLabel(x.Key),
			Count = x.Value.Count,
			UrgentCount = x.Value.Count(item => item.IsUrgent)
		}).ToList();
		var urgentItems = queueItems
			.SelectMany(x => x.Value.Where(item => item.IsUrgent).Select(item => new AdmissionCrmEnrollmentOverviewItemDto
			{
				Workspace = GetQueueWorkspace(x.Key),
				QueueType = x.Key,
				Item = item
			}))
			.OrderBy(x => x.Item.DueAt ?? DateTimeOffset.MaxValue)
			.ThenByDescending(x => x.Item.AgeDays)
			.Take(12)
			.ToList();
		var now = Clock.Now;
		var canViewOffers = permittedQueueTypes.Any(IsOfferQueue);
		var expiringOfferCount = canViewOffers
			? data.Offers.Count(x =>
				x.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened &&
				x.ValidUntil <= now.AddDays(3))
			: 0;

		return new AdmissionCrmEnrollmentOverviewDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = now,
			UnassignedLeadCount = data.Leads.Count(x => !x.OwnerUserId.HasValue),
			UpcomingExamCount = permittedQueueTypes.Any(IsExamQueue)
				? data.Exams.Count(x => x.ExamStartTime.UtcDateTime >= now &&
					x.ExamStartTime.UtcDateTime <= now.AddDays(7) &&
					x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked)
				: 0,
			ExamNoShowCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.NoShow),
			PendingPaperEntryCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PendingPaperEntry),
			ExpiringRegistrationCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ExpiringRegistrationLink),
			DocumentFollowUpCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.RejectedDocument) +
				GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.DocumentVerificationBacklog),
			FeeFollowUpCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PaymentCondition),
			PendingApprovalCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.FinalApproval) +
				GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PolicyException) +
				GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.RouteAppealReview),
			ExpiringOfferCount = expiringOfferCount,
			ClassPlacementRiskCount = canViewOffers ? data.SeatPools.Count(x => x.IsOverCapacity) : 0,
			ActivationFailureCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ActivationRepair),
			HandoffBlockerCount = GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ServiceHandoffException),
			UrgentCount = queueCounts.Sum(x => x.UrgentCount),
			Queues = queueCounts,
			UrgentItems = urgentItems
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId)
	{
		var data = await LoadDataAsync(academicYearId, nameof(GetQueueSummaryAsync));
		var permittedQueueTypes = await GetPermittedQueueTypesAsync();
		var queues = permittedQueueTypes
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
		var permittedQueueTypes = await GetPermittedQueueTypesAsync();
		if (!permittedQueueTypes.Contains(queueType))
		{
			throw new AbpAuthorizationException("The current user cannot access this admissions queue.");
		}

		var data = await LoadDataAsync(academicYearId, nameof(GetQueueAsync));
		var items = BuildQueueItems(data, queueType).AsEnumerable();
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			var applicantNumber = ParseApplicantNumber(input.Search);
			items = items.Where(x => (applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				x.ApplicantName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				(x.Details?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var applicantSearch = input.ApplicantSearch.Trim();
			var applicantNumber = ParseApplicantNumber(input.ApplicantSearch);
			items = items.Where(x =>
				(applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				x.ApplicantName.Contains(applicantSearch, StringComparison.OrdinalIgnoreCase));
		}
		if (!string.IsNullOrWhiteSpace(input.ActionSearch))
		{
			var actionSearch = input.ActionSearch.Trim();
			items = items.Where(x => x.Title.Contains(actionSearch, StringComparison.OrdinalIgnoreCase) ||
				(x.Details?.Contains(actionSearch, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (input.OwnerUserId.HasValue) items = items.Where(x => x.OwnerUserId == input.OwnerUserId);
		if (input.UrgentOnly == true) items = items.Where(x => x.IsUrgent);
		if (input.MinimumAgeDays.HasValue) items = items.Where(x => x.AgeDays >= input.MinimumAgeDays.Value);
		if (input.DueBefore.HasValue) items = items.Where(x => x.DueAt.HasValue && x.DueAt <= input.DueBefore.Value);
		if (input.RegistrationStatus.HasValue) items = items.Where(x => x.Registration?.Status == input.RegistrationStatus);
		if (input.DocumentStatus.HasValue) items = items.Where(x => x.Registration?.CurrentDocumentStatus == input.DocumentStatus);
		if (input.OfferStatus.HasValue) items = items.Where(x => x.Offer?.OfferStatus == input.OfferStatus);
		if (input.RouteType.HasValue) items = items.Where(x => x.Offer?.RouteType == input.RouteType);
		if (input.SeatPoolId.HasValue) items = items.Where(x => x.Offer?.SeatPoolId == input.SeatPoolId);
		if (input.ActivationStatus.HasValue) items = items.Where(x => x.Activation?.Status == input.ActivationStatus);
		if (input.ServiceHandoffStatus.HasValue) items = items.Where(x => x.Operation?.ServiceHandoffStatus == input.ServiceHandoffStatus);
		if (input.RegistrationLinkExpiresFrom.HasValue)
			items = items.Where(x => x.Registration?.LinkExpiresAt >= input.RegistrationLinkExpiresFrom.Value);
		if (input.RegistrationLinkExpiresUntil.HasValue)
			items = items.Where(x => x.Registration?.LinkExpiresAt < input.RegistrationLinkExpiresUntil.Value.AddDays(1));
		if (input.OfferExpiresFrom.HasValue)
			items = items.Where(x => x.Offer?.OfferExpiresAt >= input.OfferExpiresFrom.Value);
		if (input.OfferExpiresUntil.HasValue)
			items = items.Where(x => x.Offer?.OfferExpiresAt < input.OfferExpiresUntil.Value.AddDays(1));
		if (input.PaymentGateSatisfied.HasValue)
			items = items.Where(x => x.Offer?.PaymentGateSatisfied == input.PaymentGateSatisfied.Value);
		if (input.PlacementConfigured.HasValue)
			items = items.Where(x => x.Activation?.PlacementConfigured == input.PlacementConfigured.Value);
		if (input.AccountProvisioningPending.HasValue)
		{
			items = items.Where(x => x.Activation != null &&
				(x.Activation.RequireStudentAccount || x.Activation.RequireGuardianAccount) &&
				(input.AccountProvisioningPending.Value ? !x.Activation.StudentId.HasValue : x.Activation.StudentId.HasValue));
		}
		if (!string.IsNullOrWhiteSpace(input.RepairSearch))
		{
			var repairSearch = input.RepairSearch.Trim();
			items = items.Where(x =>
				(x.Activation?.RepairStep?.Contains(repairSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(x.Activation?.RepairReason?.Contains(repairSearch, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (input.AssignedToUserId.HasValue)
		{
			var delegatedOwners = await GetDelegatedOwnersAsync(academicYearId, queueType, input.AssignedToUserId.Value);
			items = items.Where(x => x.OwnerUserId == input.AssignedToUserId || (x.OwnerUserId.HasValue && delegatedOwners.Contains(x.OwnerUserId.Value)));
		}
		var filtered = ApplyQueueSorting(items, input.Sorting).ToList();
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
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(
			lead,
			nameof(GetAdmissionHandoffPackageAsync));

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
		var offerQueryable = await _offerRepository.GetQueryableAsync();
		var seatPoolQueryable = await _seatPoolRepository.GetQueryableAsync();
		var scopedLeadQuery = leadQueryable.Where(x =>
			x.TenantId == tenantId &&
			x.AcademicYearId == academicYearId);
		scopedLeadQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeadQuery);
		var leads = await AsyncExecuter.ToListAsync(scopedLeadQuery);
		var leadIds = leads.Select(x => x.Id).ToList();

		return new OperationsData(
			leads,
			await AsyncExecuter.ToListAsync(examQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(slotQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await AsyncExecuter.ToListAsync(registrationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(checklistQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(documentQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(routeQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(routeAppealQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(activationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(serviceHandoffQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(policyExceptionQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(quotationQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(identityProfileQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(decisionQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(offerQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync(seatPoolQueryable.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
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
			AdmissionCrmOperationsQueueType.RegistrationInvitationPending => BuildRegistrationInvitationPendingQueue(data),
			AdmissionCrmOperationsQueueType.PendingPaperEntry => BuildPendingPaperEntryQueue(data, leads),
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

		PopulateQueueContexts(data, queueType, items);

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

	private static List<AdmissionCrmOperationsQueueItemDto> BuildRegistrationInvitationPendingQueue(
		OperationsData data)
	{
		var registeredLeadIds = data.Registrations
			.Where(x => x.Status != AdmissionCrmRegistrationStatus.Revoked)
			.Select(x => x.LeadId)
			.ToHashSet();

		return data.Leads
			.Where(x => x.Stage == AdmissionInquiryStage.Eligible && !registeredLeadIds.Contains(x.Id))
			.Select(lead =>
			{
				var ageDays = AgeDays(lead.LastActivityTime, data.Now);
				return CreateItem(
					lead,
					$"registration-invitation:{lead.Id}",
					"Registration invitation is pending",
					"No current registration form is available. Choose a published template to continue.",
					"Ready to invite",
					null,
					ageDays,
					ageDays >= RegistrationAbandonmentWarningDays);
			})
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
			.Where(x => ((x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ||
			              x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NoShow) &&
			             x.EligibilityDecision == AdmissionCrmEligibilityDecision.Pending) ||
			            (x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked && x.ExamEndTime.UtcDateTime < data.Now))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(exam => CreateItem(
				leads[exam.LeadId],
				$"no-show:{exam.Id}",
				exam.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or AdmissionCrmExamAttendanceStatus.NoShow
					? "Entrance exam no-show"
					: "Attendance is overdue",
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

	private static List<AdmissionCrmOperationsQueueItemDto> BuildPendingPaperEntryQueue(
		OperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Registrations
			.Where(x => x.Provenance == AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper)
			.Where(x => x.DataEntryState is AdmissionCrmRegistrationDataEntryState.NotStarted or AdmissionCrmRegistrationDataEntryState.InProgress)
			.Where(x => x.Status is not (AdmissionCrmRegistrationStatus.Submitted
				or AdmissionCrmRegistrationStatus.Resubmitted
				or AdmissionCrmRegistrationStatus.Verified
				or AdmissionCrmRegistrationStatus.AdmissionReady
				or AdmissionCrmRegistrationStatus.Revoked
				or AdmissionCrmRegistrationStatus.Withdrawn))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application =>
			{
				var received = application.PhysicalFormReceivedDate.HasValue
					? $" received {application.PhysicalFormReceivedDate.Value:dd MMM yyyy}"
					: string.Empty;
				var form = string.IsNullOrWhiteSpace(application.PhysicalFormNumber)
					? application.FormNumber
					: application.PhysicalFormNumber;
				return CreateItem(
					leads[application.LeadId],
					$"paper-entry:{application.Id}",
					"Enter paper registration responses",
					$"Physical form {form}{received} is waiting for staff data entry.",
					application.DataEntryState.ToString(),
					null,
					AgeDays(application.PhysicalFormReceivedDate ?? application.IssuedAt, data.Now),
					AgeDays(application.PhysicalFormReceivedDate ?? application.IssuedAt, data.Now) >= RegistrationAbandonmentWarningDays,
					registrationApplicationId: application.Id);
			})
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
			ApplicantNumber = lead.ApplicantNumber,
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

	private static void PopulateQueueContexts(
		OperationsData data,
		AdmissionCrmOperationsQueueType queueType,
		IReadOnlyCollection<AdmissionCrmOperationsQueueItemDto> items)
	{
		var currentDocuments = GetCurrentDocuments(data.Documents).ToList();
		foreach (var item in items)
		{
			var registration = item.RegistrationApplicationId.HasValue
				? data.Registrations.FirstOrDefault(x => x.Id == item.RegistrationApplicationId.Value)
				: data.Registrations.Where(x => x.LeadId == item.LeadId)
					.OrderByDescending(x => x.IssuedAt).FirstOrDefault();
			var checklist = registration == null
				? null
				: data.Checklists.Where(x => x.RegistrationApplicationId == registration.Id)
					.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
			IReadOnlyList<AdmissionCrmApplicantDocument> documents = checklist == null
				? []
				: currentDocuments.Where(x => x.ChecklistAssignmentId == checklist.Id).ToList();
			var currentDocument = item.DocumentId.HasValue
				? documents.FirstOrDefault(x => x.Id == item.DocumentId.Value)
				: null;
			if (registration != null || IsRegistrationQueue(queueType))
			{
				var resolvedCount = documents.Count(x => x.Status is AdmissionCrmApplicantDocumentStatus.Verified
					or AdmissionCrmApplicantDocumentStatus.Waived
					or AdmissionCrmApplicantDocumentStatus.OriginalSeen);
				item.Registration = new AdmissionCrmRegistrationQueueContextDto
				{
					RegistrationApplicationId = registration?.Id,
					Status = registration?.Status,
					FormNumber = registration?.FormNumber,
					Provenance = registration?.Provenance,
					DataEntryState = registration?.DataEntryState,
					PhysicalFormNumber = registration?.PhysicalFormNumber,
					PhysicalFormReceivedDate = registration?.PhysicalFormReceivedDate,
					CompletionPercentage = registration?.CompletionPercentage ?? 0,
					LinkExpiresAt = registration?.LinkExpiresAt,
					FeeStatus = registration?.FeeStatus,
					DocumentTotalCount = documents.Count,
					DocumentResolvedCount = resolvedCount,
					DocumentPendingCount = Math.Max(0, documents.Count - resolvedCount),
					CurrentDocumentStatus = currentDocument?.Status,
					ReadinessConfirmed = checklist?.ReadinessConfirmedAt.HasValue == true
				};
			}

			var offer = data.Offers.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.Version).ThenByDescending(x => x.CreationTime).FirstOrDefault();
			var decision = data.Decisions.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.DecidedAt ?? x.CreationTime).FirstOrDefault();
			var route = data.Routes.Where(x => x.LeadId == item.LeadId && x.IsPrimary)
				.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
			var quotation = data.Quotations.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.Version).FirstOrDefault();
			var seatPoolId = offer?.SeatPoolId ?? decision?.SeatPoolId;
			var seatPool = seatPoolId.HasValue
				? data.SeatPools.FirstOrDefault(x => x.Id == seatPoolId.Value)
				: null;
			if (offer != null || decision != null || route != null || quotation != null || IsOfferQueue(queueType))
			{
				item.Offer = new AdmissionCrmOfferQueueContextDto
				{
					OfferId = offer?.Id,
					OfferNumber = offer?.OfferNumber,
					OfferStatus = offer?.Status,
					OfferExpiresAt = offer?.ValidUntil,
					SeatPoolId = seatPoolId,
					SeatPoolName = seatPool?.Name,
					RemainingSeats = seatPool?.RemainingCapacity,
					DecisionStatus = decision?.Status,
					RouteType = route?.RouteType,
					RouteStatus = route?.Status,
					FinanceState = quotation?.FinanceState,
					PaymentGateSatisfied = quotation == null ? null : quotation.IsGateSatisfiedAt(data.Now)
				};
			}

			var activation = data.Activations.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.RequestedAt).FirstOrDefault();
			if (activation != null || IsActivationQueue(queueType))
			{
				item.Activation = new AdmissionCrmActivationQueueContextDto
				{
					ActivationId = activation?.Id,
					StudentId = activation?.StudentId,
					ActivationReference = activation?.ActivationReference,
					Status = activation?.Status,
					PlacementConfigured = activation != null && activation.StandardId != Guid.Empty && activation.DivisionId != Guid.Empty,
					RollNumber = activation?.RollNumber,
					RequireStudentAccount = activation?.RequireStudentAccount ?? false,
					RequireGuardianAccount = activation?.RequireGuardianAccount ?? false,
					AttemptCount = activation?.AttemptCount ?? 0,
					RepairStep = activation?.RepairStep,
					RepairReason = activation?.RepairReason
				};
			}

			var handoff = item.ServiceHandoffId.HasValue
				? data.ServiceHandoffs.FirstOrDefault(x => x.Id == item.ServiceHandoffId.Value)
				: null;
			item.Operation = new AdmissionCrmOperationQueueContextDto
			{
				QueueType = queueType,
				Severity = item.IsUrgent ? "Urgent" : item.AgeDays > 0 ? "Attention" : "Normal",
				AssignmentStatus = item.OwnerUserId.HasValue ? "Assigned" : "Unassigned",
				ServiceHandoffStatus = handoff?.Status,
				RetryAttemptCount = handoff?.AttemptCount
			};
		}
	}

	private static bool IsRegistrationQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.RegistrationInvitationPending or
		AdmissionCrmOperationsQueueType.PendingPaperEntry or
		AdmissionCrmOperationsQueueType.ExpiringRegistrationLink or
		AdmissionCrmOperationsQueueType.IncompleteRegistration or
		AdmissionCrmOperationsQueueType.RejectedDocument or
		AdmissionCrmOperationsQueueType.DocumentVerificationBacklog or
		AdmissionCrmOperationsQueueType.AdmissionReady;

	private static bool IsExamQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.UnassignedExam or
		AdmissionCrmOperationsQueueType.CapacityConflict or
		AdmissionCrmOperationsQueueType.NoShow;

	private static bool IsOfferQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.FinalApproval or
		AdmissionCrmOperationsQueueType.PolicyException or
		AdmissionCrmOperationsQueueType.PaymentCondition or
		AdmissionCrmOperationsQueueType.RouteEligibilityReview or
		AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation or
		AdmissionCrmOperationsQueueType.RouteAppealReview;

	private static bool IsActivationQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.ActivationRepair or
		AdmissionCrmOperationsQueueType.CancellationReview or
		AdmissionCrmOperationsQueueType.ServiceHandoffException;

	private static AdmissionCrmMyWorkItemDto CreateTaskWorkItem(
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

	private static AdmissionCrmMyWorkItemDto CreateBlockerWorkItem(
		AdmissionCrmOperationsQueueType queueType,
		AdmissionCrmOperationsQueueItemDto item,
		AdmissionInquiryLead lead,
		Guid assignedUserId,
		DateTime now)
	{
		var action = GetRecommendedAction(queueType, item);
		return new AdmissionCrmMyWorkItemDto
		{
			Key = $"queue:{(int)queueType}:{item.Key}",
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

	private static AdmissionCrmRecommendedAction GetRecommendedAction(
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

	private static AdmissionCrmWorkspaceSection GetSectionForQueue(AdmissionCrmOperationsQueueType queueType)
	{
		if (IsExamQueue(queueType)) return AdmissionCrmWorkspaceSection.EntranceExam;
		if (IsRegistrationQueue(queueType)) return AdmissionCrmWorkspaceSection.Registration;
		if (IsOfferQueue(queueType)) return AdmissionCrmWorkspaceSection.Offer;
		if (IsActivationQueue(queueType)) return AdmissionCrmWorkspaceSection.Activation;
		return AdmissionCrmWorkspaceSection.Governance;
	}

	private static AdmissionCrmWorkspaceSection GetSectionForStage(AdmissionInquiryStage stage)
	{
		if (stage is AdmissionInquiryStage.ExamReady or AdmissionInquiryStage.ExamScheduled or AdmissionInquiryStage.ExamCompleted)
			return AdmissionCrmWorkspaceSection.EntranceExam;
		return AdmissionCrmWorkspaceSection.Overview;
	}

	private static AdmissionCrmWorkUrgency GetUrgency(DateTimeOffset? dueAt, DateTime now)
	{
		if (!dueAt.HasValue) return AdmissionCrmWorkUrgency.Normal;
		var dueDate = dueAt.Value.UtcDateTime.Date;
		var today = now.ToUniversalTime().Date;
		if (dueDate < today) return AdmissionCrmWorkUrgency.Overdue;
		if (dueDate == today) return AdmissionCrmWorkUrgency.DueToday;
		if (dueDate <= today.AddDays(3)) return AdmissionCrmWorkUrgency.DueSoon;
		return AdmissionCrmWorkUrgency.Normal;
	}

	private static IEnumerable<AdmissionCrmMyWorkItemDto> ApplyMyWorkSorting(
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

	private static IEnumerable<AdmissionCrmOperationsQueueItemDto> ApplyQueueSorting(
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

	private static int GetPriorityRank(AdmissionCrmLeadPriority priority) => priority switch
	{
		AdmissionCrmLeadPriority.Hot => 0,
		AdmissionCrmLeadPriority.Warm => 1,
		AdmissionCrmLeadPriority.Cold => 2,
		AdmissionCrmLeadPriority.Low => 3,
		_ => 4
	};

	private static string GetUserDisplayName(IdentityUser user)
	{
		var fullName = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
	}

	private async Task<IReadOnlyList<AdmissionCrmOperationsQueueType>> GetPermittedQueueTypesAsync()
	{
		var queueTypes = Enum.GetValues<AdmissionCrmOperationsQueueType>().ToHashSet();
		if (!await IsAnyPermissionGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishExamResults))
		{
			queueTypes.RemoveWhere(IsExamQueue);
		}
		if (!await IsAnyPermissionGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness))
		{
			queueTypes.RemoveWhere(IsRegistrationQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans))
		{
			queueTypes.RemoveWhere(IsOfferQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.PaymentCondition);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation))
		{
			queueTypes.RemoveWhere(IsActivationQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.RouteEligibilityReview);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.TransferEvidencePending);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.ReturningIdentityMatch);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.RouteAppealReview);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.HouseholdResolution);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.IdentityConflict);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.PolicyException);
		}
		return queueTypes.OrderBy(x => (int)x).ToList();
	}

	private async Task<bool> IsAnyPermissionGrantedAsync(params string[] permissions)
	{
		foreach (var permission in permissions)
		{
			if (await AuthorizationService.IsGrantedAsync(permission)) return true;
		}
		return false;
	}

	private static int GetQueueCount(
		IReadOnlyDictionary<AdmissionCrmOperationsQueueType, IReadOnlyList<AdmissionCrmOperationsQueueItemDto>> queues,
		AdmissionCrmOperationsQueueType queueType) =>
		queues.TryGetValue(queueType, out var items) ? items.Count : 0;

	private static string GetQueueWorkspace(AdmissionCrmOperationsQueueType queueType)
	{
		if (IsRegistrationQueue(queueType)) return "registrations";
		if (IsOfferQueue(queueType)) return "offers";
		if (IsActivationQueue(queueType)) return "activation";
		return queueType is AdmissionCrmOperationsQueueType.UnassignedExam or
			AdmissionCrmOperationsQueueType.CapacityConflict or AdmissionCrmOperationsQueueType.NoShow
			? "exams"
			: "operations";
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

	private static int? ParseApplicantNumber(string? value)
	{
		var normalized = value?.Trim().TrimStart('#');
		return int.TryParse(normalized, out var applicantNumber) && applicantNumber > 0
			? applicantNumber
			: null;
	}

	private static string GetQueueLabel(AdmissionCrmOperationsQueueType queueType)
	{
		return queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => "Unassigned exams",
			AdmissionCrmOperationsQueueType.CapacityConflict => "Capacity conflicts",
			AdmissionCrmOperationsQueueType.NoShow => "No-shows and overdue attendance",
			AdmissionCrmOperationsQueueType.RegistrationInvitationPending => "Eligible applicants awaiting registration",
			AdmissionCrmOperationsQueueType.PendingPaperEntry => "Pending paper registration entry",
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
		IReadOnlyList<AdmissionCrmSeatOffer> Offers,
		IReadOnlyList<AdmissionCrmSeatPool> SeatPools,
		DateTime Now);
}
