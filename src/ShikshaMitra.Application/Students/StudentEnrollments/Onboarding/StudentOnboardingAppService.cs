using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[Authorize]
public partial class StudentOnboardingAppService(
	IRepository<StudentOnboardingCase, Guid> caseRepository,
	IRepository<StudentOnboardingStep, Guid> stepRepository,
	IRepository<StudentOnboardingEvent, Guid> eventRepository,
	IRepository<StudentOnboardingPolicy, Guid> policyRepository,
	IRepository<StudentOnboardingSettings, Guid> settingsRepository,
	OnboardingConfigurationManager configurationManager,
	ShikshaMitra.Security.UserScope.IUserScopeService userScopeService,
	OnboardingFeeReadinessReader feeReadinessReader,
	AdmissionFeeSelectionOnboardingReader admissionFeeSelectionReader,
	IRepository<StudentSeatReservation, Guid> reservationRepository,
	IRepository<StudentIdentifierSeries, Guid> seriesRepository,
	IRepository<StudentIdentifierAllocation, Guid> allocationRepository,
	IRepository<StudentOnboardingServiceHandoff, Guid> handoffRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCampus, Guid> campusRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<ShikshaMitra.TenantSettings.EducationBoards.EducationBoard, Guid> boardRepository,
	IRepository<ShikshaMitra.TenantSettings.EducationMediums.EducationMedium, Guid> mediumRepository,
	IRepository<ShikshaMitra.TenantSettings.EducationBoards.EducationMediumStage, Guid> mediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	IStudentRepository studentRepository,
	StudentDocumentManager studentDocumentManager,
	AdmissionStudentEvidenceReader admissionEvidenceReader,
	IIdentityUserRepository identityUserRepository,
	IdentityUserManager portalUserManager,
	IdentityRoleManager portalRoleManager,
	IRepository<ShikshaMitra.Students.Portal.StudentPortalAccountLink, Guid> portalLinkRepository,
	ShikshaMitra.Students.Portal.StudentPortalAccessService portalAccess,
	IDistributedEventBus distributedEventBus,
	IUnitOfWorkManager unitOfWorkManager)
	: ApplicationService, IStudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<PagedResultDto<StudentOnboardingCaseDto>> GetListAsync(StudentOnboardingListInput input)
	{
		var query = await GetScopedCasesAsync();
		if (input.EnteredToday == true)
		{
			var from = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now).Date.AddHours(-5.5);
			var until = from.AddDays(1);
			query = query.Where(item => item.CreationTime >= from && item.CreationTime < until);
		}
		if (input.Status.HasValue) query = query.Where(item => item.Status == input.Status.Value);
		if (input.AcademicYearId.HasValue) query = query.Where(item => item.AcademicYearId == input.AcademicYearId.Value);
		if (input.BoardId.HasValue) query = query.Where(item => item.IntendedBoardId == input.BoardId.Value);
		if (input.MediumId.HasValue) query = query.Where(item => item.IntendedMediumId == input.MediumId.Value);
		if (input.StandardId.HasValue) query = query.Where(item => item.IntendedStandardId == input.StandardId.Value);
		if (input.DivisionId.HasValue) query = query.Where(item => item.DivisionId == input.DivisionId.Value);
		if (input.StandardIds.Count > 0) query = query.Where(item => Enumerable.Contains(input.StandardIds, item.IntendedStandardId));
		if (input.DivisionIds.Count > 0) query = query.Where(item => item.DivisionId.HasValue && Enumerable.Contains(input.DivisionIds, item.DivisionId.Value));
		if (input.OverdueOnly == true)
			query = query.Where(item => item.Status != StudentOnboardingStatus.Completed &&
				item.Status != StudentOnboardingStatus.Cancelled && item.DueAt < DateTime.UtcNow);

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			var applications = await applicationRepository.GetQueryableAsync();
			var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			query = from onboarding in query
				join application in applications on onboarding.ApplicationId equals application.Id
				join student in students on onboarding.StudentId equals student.Id
				where application.ApplicationNumber.ToLower().Contains(search) ||
				      (student.FirstName != null && student.FirstName.ToLower().Contains(search)) ||
				      (student.LastName != null && student.LastName.ToLower().Contains(search))
				select onboarding;
		}

		query = await FilterDashboardQueueAsync(query, input.DashboardQueue);
		query = await FilterPendingTaskAsync(query, input.PendingTask);
		var worklist = await GetWorklistQueryAsync(query, input);
		var totalCount = await AsyncExecuter.CountAsync(worklist);
		var cases = await AsyncExecuter.ToListAsync(SortWorklist(worklist, input.Sorting)
			.Skip(input.SkipCount).Take(input.MaxResultCount).Select(row => row.Case));
		var rows = new List<StudentOnboardingCaseDto>(cases.Count);
		var configurations = new Dictionary<Guid, OnboardingRuleConfiguration?>();
		foreach (var year in cases.Where(item => !item.StudentEnrollmentId.HasValue).Select(item => item.AcademicYearId).Distinct())
			configurations[year] = await configurationManager.GetAsync(year);
		foreach (var onboardingCase in cases)
			rows.Add(await MapCaseAsync(onboardingCase, includeEvents: false, configurations: configurations));
		return new PagedResultDto<StudentOnboardingCaseDto>(totalCount, rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingFilterOptionsDto> GetFilterOptionsAsync(Guid? academicYearId = null)
	{
		var query = await GetScopedCasesAsync();
		if (academicYearId.HasValue) query = query.Where(item => item.AcademicYearId == academicYearId.Value);
		var academicYearIds = await AsyncExecuter.ToListAsync(query
			.Select(item => item.AcademicYearId).Distinct());
		var standardIds = await AsyncExecuter.ToListAsync(query
			.Select(item => item.IntendedStandardId).Distinct());
		var boardIds = await AsyncExecuter.ToListAsync(query.Select(item => item.IntendedBoardId).Distinct());
		var mediumIds = await AsyncExecuter.ToListAsync(query.Select(item => item.IntendedMediumId).Distinct());
		var divisionIds = await AsyncExecuter.ToListAsync(query.Where(item => item.DivisionId.HasValue)
			.Select(item => item.DivisionId!.Value).Distinct());
		// Dropdown labels must not materialize the recursively auto-included academic graph.
		// Keep every lookup bounded by IDs from the already tenant/user-scoped cases.
		var boards = await AsyncExecuter.ToListAsync((await boardRepository.GetQueryableAsync())
			.Where(item => boardIds.Contains(item.Id)).Select(item => new { item.Id, item.Type }));
		var mediums = await AsyncExecuter.ToListAsync((await mediumRepository.GetQueryableAsync())
			.Where(item => mediumIds.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.EducationBoardId }));
		var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
			.Where(item => divisionIds.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.StandardId, item.SortOrder }));
		var standards = standardIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
				.Where(item => standardIds.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.EducationMediumStageId, item.SortOrder,
					MediumId = item.EducationMediumStage.EducationMediumId, StageSortOrder = item.EducationMediumStage.SortOrder, StageName = item.EducationMediumStage.Name }));
		var stageIds = standards.Select(item => item.EducationMediumStageId).Distinct().ToList();
		var mediumByStage = (await AsyncExecuter.ToListAsync((await mediumStageRepository.GetQueryableAsync())
			.Where(item => stageIds.Contains(item.Id)).Select(item => new { item.Id, item.EducationMediumId })))
			.ToDictionary(item => item.Id, item => item.EducationMediumId);
		var academicYears = academicYearIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync((await academicYearRepository.GetQueryableAsync())
				.Where(item => academicYearIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }));
		return new StudentOnboardingFilterOptionsDto
		{
            CanUnlockIdentifiers = CurrentUser.Id.HasValue && (await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value)).IsAdmin &&
                await LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Authorization.Permissions.IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair),
			AcademicYears = academicYears.OrderByDescending(item => item.Name)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name }).ToList(),
			Boards = boards.OrderBy(item => item.Type)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Type.ToString() }).ToList(),
			Mediums = mediums.OrderBy(item => item.Name)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name,
					ParentId = item.EducationBoardId }).ToList(),
			Standards = standards.OrderBy(item => item.MediumId).ThenBy(item => item.StageSortOrder).ThenBy(item => item.StageName).ThenBy(item => item.EducationMediumStageId).ThenBy(item => item.SortOrder).ThenBy(item => item.Name).ThenBy(item => item.Id)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name,
					ParentId = mediumByStage.GetValueOrDefault(item.EducationMediumStageId) }).ToList(),
			Divisions = divisions.OrderBy(item => item.StandardId).ThenBy(item => item.SortOrder).ThenBy(item => item.Name).ThenBy(item => item.Id)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name,
					ParentId = item.StandardId }).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingAnalyticsDto> GetAnalyticsAsync()
	{
		var query = await GetScopedCasesAsync();
		var now = DateTime.UtcNow;
		return new StudentOnboardingAnalyticsDto
		{
			ActiveCases = await AsyncExecuter.CountAsync(query.Where(item =>
				item.Status != StudentOnboardingStatus.Completed && item.Status != StudentOnboardingStatus.Cancelled)),
			Pending = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.Pending)),
			InProgress = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.InProgress)),
			ReadyForActivation = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.ReadyForActivation)),
			NeedsAttention = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.NeedsAttention)),
			Overdue = await AsyncExecuter.CountAsync(query.Where(item =>
				item.Status != StudentOnboardingStatus.Completed && item.Status != StudentOnboardingStatus.Cancelled && item.DueAt < now)),
			Completed = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.Completed)),
			Cancelled = await AsyncExecuter.CountAsync(query.Where(item => item.Status == StudentOnboardingStatus.Cancelled))
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingCaseDto> GetAsync(Guid id) =>
		await MapCaseAsync(await GetCaseAsync(id), includeEvents: true);

	[RemoteService(false)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task EnsureCaseAccessAsync(Guid id)
	{
		var item = await GetCaseAsync(id);
		if (await CaptureAutomaticRequirementsAsync(item))
		{
			await RefreshProfileReadinessAsync(item, await stepRepository.GetListAsync(step => step.OnboardingCaseId == id),
				Clock.Now, requirementsCaptured: true);
			await CurrentUnitOfWork!.SaveChangesAsync();
		}
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentOnboardingPlacementOptionDto>> GetPlacementOptionsAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var divisionQuery = await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>()
			.ApplyCurrentUserDivisionScopeAsync(await divisionRepository.GetQueryableAsync(), onboardingCase.AcademicYearId);
		var divisions = await AsyncExecuter.ToListAsync(divisionQuery.Where(item => item.StandardId == onboardingCase.IntendedStandardId));
		var now = DateTime.UtcNow;
		var options = new List<StudentOnboardingPlacementOptionDto>(divisions.Count);
		foreach (var division in divisions.OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ThenBy(item => item.Id))
		{
			var activeEnrollments = await enrollmentRepository.CountAsync(item =>
				item.AcademicYearId == onboardingCase.AcademicYearId && item.DivisionId == division.Id && item.IsActive);
			var reservations = await reservationRepository.GetListAsync(item =>
				item.AcademicYearId == onboardingCase.AcademicYearId && item.DivisionId == division.Id &&
				item.Status == StudentSeatReservationStatus.Reserved);
			var liveReservations = reservations.Count(item => item.IsLive(now));
			options.Add(new StudentOnboardingPlacementOptionDto
			{
				Id = division.Id,
				Name = division.Name,
				Capacity = division.Strength,
				ActiveEnrollments = activeEnrollments,
				LiveReservations = liveReservations,
				AvailableSeats = Math.Max(0, division.Strength - activeEnrollments - liveReservations)
			});
		}
		return options;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetAvailableIdentifierSeriesAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var now = DateTime.UtcNow;
		var configuration = await configurationManager.GetAsync(onboardingCase.AcademicYearId);
		if (configuration != null)
		{
			var configured = await GetConfiguredSeriesAsync(configuration, onboardingCase, false);
			return configured == null ? [] : [MapSeries(configured)];
		}
		return (await seriesRepository.GetListAsync(item =>
			item.AcademicYearId == onboardingCase.AcademicYearId &&
			(item.IdentifierType == StudentIdentifierType.GrNumber ||
			 item.IdentifierType == StudentIdentifierType.AdmissionNumber) && item.IsActive &&
			(!item.StandardId.HasValue || item.StandardId == onboardingCase.IntendedStandardId) &&
			item.EffectiveFrom <= now && (!item.EffectiveUntil.HasValue || item.EffectiveUntil >= now) &&
			(!item.MaximumSequence.HasValue || item.NextSequence <= item.MaximumSequence.Value)))
			.OrderByDescending(item => item.StandardId == onboardingCase.IntendedStandardId)
			.ThenBy(item => item.Code)
			.Select(MapSeries)
			.ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<StudentOnboardingCaseDto> RefreshReadinessAsync(Guid id, string? concurrencyStamp)
	{
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, concurrencyStamp);
		if (onboardingCase.StudentEnrollmentId.HasValue)
		{
			await LinkSiblingParentAsync(onboardingCase);
			await PrepareAutomaticPortalAccountsAsync(onboardingCase);
			await RefreshProvisioningAsync(onboardingCase);
			return await MapCaseAsync(onboardingCase, true);
		}
		var steps = await stepRepository.GetListAsync(item => item.OnboardingCaseId == id);
		var now = DateTime.UtcNow;
		await RefreshProfileReadinessAsync(onboardingCase, steps, now);
		onboardingCase.RecordProgress(now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "ReadinessRefreshed", "Profile, family, communication, document, and medical readiness were recalculated from Student records.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true, steps);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public Task<StudentOnboardingCaseDto> AssignOwnerAsync(Guid id, AssignStudentOnboardingOwnerDto input)
	{
		throw new Volo.Abp.BusinessException("Admissions.StaffAssignmentRetired", "Staff handling is recorded automatically in the activity history.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<StudentOnboardingCaseDto> SetAdmissionRouteAsync(Guid id, SetStudentOnboardingRouteDto input)
	{
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		var requestedRoute = input.AdmissionRoute?.Trim();
		var configuration = await configurationManager.GetAsync(item.AcademicYearId);
		var configuredRoute = configuration?.FindAdmissionRoute(requestedRoute);
		var builtInRoute = OnboardingRuleConfiguration.FindBuiltInAdmissionRoute(requestedRoute);
		if (configuredRoute == null && builtInRoute == null)
			throw OnboardingRuleConfiguration.Invalid("Choose an admission route configured for this academic year.");
		item.SetAdmissionRoute(configuredRoute ?? builtInRoute!);
		var steps = await stepRepository.GetListAsync(s => s.OnboardingCaseId == id);
		steps.SingleOrDefault(s => s.Key == StudentOnboardingStepKeys.ApprovalReview)?.Reset("Admission route changed; review the applicable requirements again.");
		await RefreshProfileReadinessAsync(item, steps, DateTime.UtcNow);
		await caseRepository.UpdateAsync(item, false);
		await AddEventAsync(id, "AdmissionRouteChanged", $"Onboarding route changed to {item.GetAdmissionRoute()}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(item, true, steps);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public async Task<StudentOnboardingCaseDto> UpdateStepAsync(
		Guid id, string stepKey, UpdateStudentOnboardingStepDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		var step = await stepRepository.FindAsync(item => item.OnboardingCaseId == id && item.Key == stepKey) ??
			throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
		var now = DateTime.UtcNow;
		switch (input.Status)
		{
			case StudentOnboardingStepStatus.Pending:
			case StudentOnboardingStepStatus.NotRequired:
				step.Reset(input.Detail);
				break;
			case StudentOnboardingStepStatus.InProgress:
				step.Begin(input.Detail);
				break;
			case StudentOnboardingStepStatus.Completed:
				if (step.Key == StudentOnboardingStepKeys.FinalReadiness || IsProfileReadinessStep(step.Key) ||
				    step.Key is StudentOnboardingStepKeys.PlacementAndSeat or StudentOnboardingStepKeys.IdentifiersAndRoll)
					throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
				if (step.Key == StudentOnboardingStepKeys.ApprovalReview)
				{
					await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate);
					if (string.IsNullOrWhiteSpace(input.Detail)) throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
					var approvedRules = await ReadSavedRulesAsync(onboardingCase);
                    onboardingCase.ExtraProperties["ApprovalConfigurationHash"] = approvedRules.HasValue
                        ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(approvedRules.Value.GetRawText()))) : "legacy";
				}
				step.Complete(CurrentUser.Id, now, input.Detail);
				break;
			case StudentOnboardingStepStatus.Blocked:
				step.Block(input.Detail ?? "Action is required before this step can continue.");
				break;
			case StudentOnboardingStepStatus.Waived:
				if (step.Required) throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
				await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Waive);
				if (!CurrentUser.Id.HasValue || string.IsNullOrWhiteSpace(input.WaiverReason))
					throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
				step.Waive(CurrentUser.Id.Value, now, input.WaiverReason);
				break;
			default:
				throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
		}

		onboardingCase.RecordProgress(now);
		await stepRepository.UpdateAsync(step, false);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "StepChanged", $"{step.Label} changed to {step.Status}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	public async Task<StudentOnboardingCaseDto> SetPlacementAsync(Guid id, SetStudentOnboardingPlacementDto input)
	{
		try { return await SetPlacementCoreAsync(id, input); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			throw new BusinessException("Students:Records:RollProposalChanged");
		}
	}

	private async Task<StudentOnboardingCaseDto> SetPlacementCoreAsync(Guid id, SetStudentOnboardingPlacementDto input)
	{
		using var placementWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		var division = await divisionRepository.FindAsync(input.DivisionId);
		if (division == null || division.StandardId != onboardingCase.IntendedStandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);

		var now = DateTime.UtcNow;
		var placementChanged = onboardingCase.DivisionId.HasValue && onboardingCase.DivisionId != division.Id;
		if (placementChanged)
		{
			onboardingCase.ExtraProperties["PlacementChanged"] = true;
			onboardingCase.ExtraProperties["PlacementChangeReason"] = input.Reason?.Trim();
		}
		if (onboardingCase.SeatReservationId.HasValue)
		{
			var current = await reservationRepository.FindAsync(onboardingCase.SeatReservationId.Value);
			if (current?.DivisionId == division.Id && current.IsLive(now))
			{
				var existing = await MapCaseAsync(onboardingCase, true);
				await placementWork.CompleteAsync();
				return existing;
			}
			if (current != null)
			{
				current.Release("Placement changed", now);
				await reservationRepository.UpdateAsync(current, false);
			}
		}

		var activeEnrollments = await enrollmentRepository.CountAsync(item => item.AcademicYearId == onboardingCase.AcademicYearId &&
			item.DivisionId == division.Id && item.IsActive);
		var reservations = await reservationRepository.GetListAsync(item =>
			item.AcademicYearId == onboardingCase.AcademicYearId && item.DivisionId == division.Id &&
			item.Status == StudentSeatReservationStatus.Reserved);
		foreach (var expired in reservations.Where(item => !item.IsLive(now)))
		{
			expired.Expire(now);
			await reservationRepository.UpdateAsync(expired, false);
		}
		var liveReservations = reservations.Where(item => item.IsLive(now) && item.OnboardingCaseId != id).ToList();
		if (activeEnrollments + liveReservations.Count >= division.Strength)
			throw new BusinessException(StudentOnboardingErrorCodes.DivisionFull);
		var unavailableSeats = new HashSet<int>(Enumerable.Range(1, activeEnrollments));
		unavailableSeats.UnionWith(liveReservations.Select(item => item.SeatNumber));
		var seatNumber = Enumerable.Range(1, division.Strength).First(number => !unavailableSeats.Contains(number));
		var policy = await ResolvePolicyAsync(onboardingCase);
		var reservation = new StudentSeatReservation(GuidGenerator.Create(), CurrentTenant.Id, id,
			onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId, division.Id, seatNumber, now,
			now.AddMinutes(policy?.SeatReservationMinutes ?? 1440));
		await reservationRepository.InsertAsync(reservation, false);
		if (placementChanged && onboardingCase.RollNumber.HasValue)
			await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().RecordIssuedAsync(
				onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId, onboardingCase.DivisionId!.Value, onboardingCase.RollNumber.Value);
		onboardingCase.RecordPlacement(division.Id, reservation.Id, now);
        if (placementChanged && onboardingCase.RollNumber.HasValue && onboardingCase.IsIdentifierLocked("roll"))
        {
            var rollReader = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>();
            await rollReader.ValidateAsync(onboardingCase.StudentId, onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId,
                division.Id, onboardingCase.RollNumber, input.Reason ?? "", await rollReader.CaptureForCaseAsync(onboardingCase));
        }
		await InvalidateApprovalAsync(id, "Placement changed; review onboarding again.");
		if (placementChanged && onboardingCase.RollNumber.HasValue && !onboardingCase.IsIdentifierLocked("roll"))
		{
			onboardingCase.ClearRollNumberForPlacementChange(now);
			await ResetSystemStepAsync(id, StudentOnboardingStepKeys.IdentifiersAndRoll,
				"Division changed; select a roll number that is available in the new division.");
		}
		await PrepareForLockedGroupAsync(onboardingCase);
		await RefreshIdentifierStepAsync(onboardingCase);
		await CompleteSystemStepAsync(id, StudentOnboardingStepKeys.PlacementAndSeat,
			$"Division {division.Name} reserved (capacity slot {seatNumber}).", now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "SeatReserved", $"Division {division.Name} reserved until {reservation.ExpiresAt:u}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await MapCaseAsync(onboardingCase, true);
		await placementWork.CompleteAsync();
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<StudentOnboardingCaseDto> AllocateIdentifierAsync(
		Guid id, AllocateStudentOnboardingIdentifierDto input)
	{
		try { return await AllocateIdentifierCoreAsync(id, input); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			throw new BusinessException("Students:Records:RollProposalChanged");
		}
	}
	private async Task<StudentOnboardingCaseDto> AllocateIdentifierCoreAsync(Guid id, AllocateStudentOnboardingIdentifierDto input)
	{
		using var rollWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		if (!onboardingCase.DivisionId.HasValue)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		if (onboardingCase.RollNumber.HasValue && onboardingCase.RollNumber != input.RollNumber && onboardingCase.IsIdentifierLocked("roll")) throw new BusinessException("Students:Onboarding:IdentifierLocked");
		var frozenRollRule = await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().CaptureForCaseAsync(onboardingCase);
		var rollMode = StudentOnboardingRollNumberAllocator.ResolveMode(frozenRollRule, input.RollAssignmentMode);
		if (rollMode == "automatic")
		{
			var proposal = await ProposeCaseRollAsync(onboardingCase, input.RollNumber, input.RollAssignmentMode, input.OverrideReason, frozenRollRule);
			if (input.RollNumber != proposal.RollNumber) throw new BusinessException("Students:Records:RollProposalChanged").WithData("ProposedRollNumber", proposal.RollNumber);
		}
		else await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().ValidateAsync(onboardingCase.StudentId, onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId, onboardingCase.DivisionId.Value, input.RollNumber, input.OverrideReason ?? "", frozenRollRule);
		if (input.RollNumber <= 0) throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
		if (await enrollmentRepository.AnyAsync(item => item.AcademicYearId == onboardingCase.AcademicYearId &&
			item.DivisionId == onboardingCase.DivisionId && item.RollNumber == input.RollNumber && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
		if (await caseRepository.AnyAsync(item => item.Id != id &&
			item.AcademicYearId == onboardingCase.AcademicYearId && item.DivisionId == onboardingCase.DivisionId &&
			item.RollNumber == input.RollNumber && item.Status != StudentOnboardingStatus.Completed &&
			item.Status != StudentOnboardingStatus.Cancelled))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);

		var now = DateTime.UtcNow;
		var student = await studentRepository.FindAsync(onboardingCase.StudentId) ??
			throw new EntityNotFoundException(typeof(Student), onboardingCase.StudentId);
		var configuration = await configurationManager.GetAsync(onboardingCase.AcademicYearId);
		var grRule = configuration?.ResolveRule("grNumber", onboardingCase, now);
		var rollRule = configuration?.ResolveRule("rollNumber", onboardingCase, now);
		if (onboardingCase.RollNumber.HasValue && onboardingCase.RollNumber != input.RollNumber && rollRule.HasValue &&
			OnboardingRuleConfiguration.Boolean(rollRule.Value, "requireChangeReason") && string.IsNullOrWhiteSpace(input.OverrideReason))
			throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
		if (rollRule.HasValue && input.RollNumber < OnboardingRuleConfiguration.Number(rollRule.Value, "startingNumber", 1))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
		if (rollRule.HasValue && OnboardingRuleConfiguration.Text(rollRule.Value, "numberingScope") == "class" &&
		    (await enrollmentRepository.AnyAsync(item => item.AcademicYearId == onboardingCase.AcademicYearId &&
			 item.StandardId == onboardingCase.IntendedStandardId && item.RollNumber == input.RollNumber && item.IsActive) ||
		     await caseRepository.AnyAsync(item => item.Id != id && item.AcademicYearId == onboardingCase.AcademicYearId &&
			 item.IntendedStandardId == onboardingCase.IntendedStandardId && item.RollNumber == input.RollNumber &&
			 item.Status != StudentOnboardingStatus.Completed && item.Status != StudentOnboardingStatus.Cancelled)))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
		Guid? allocationId = onboardingCase.IdentifierAllocationId;
		if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
		{
			if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(onboardingCase).HasValue)
				throw new BusinessException("Students:Records:RenewalChecklistRequired").WithData("Reason", "Verify the permanent GR number through Student Records before preparing a return.");
			var existing = await allocationRepository.FindAsync(item => item.OnboardingCaseId == id &&
				item.Status == StudentIdentifierAllocationStatus.Reserved);
			if (existing != null)
			{
				allocationId = existing.Id;
			}
			else
			{
				var series = (configuration == null ? await seriesRepository.FindAsync(input.NumberSeriesId) :
					await GetConfiguredSeriesAsync(configuration, onboardingCase, true)) ??
					throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
				if (series.Id != input.NumberSeriesId || !series.IsActive ||
				    configuration == null && series.AcademicYearId != onboardingCase.AcademicYearId ||
				    series.IdentifierType is not (StudentIdentifierType.GrNumber or StudentIdentifierType.AdmissionNumber) ||
				    series.StandardId.HasValue && series.StandardId != onboardingCase.IntendedStandardId)
					throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
				series.EnsureAvailable(now);
				long sequence;
				string value;
				var isManual = !string.IsNullOrWhiteSpace(input.ManualValue);
				if (isManual)
				{
					if (configuration != null && (OnboardingRuleConfiguration.Text(configuration.Root.GetProperty("grNumber"), "assignmentMode") == "automatic" ||
						!grRule.HasValue || !OnboardingRuleConfiguration.Boolean(grRule.Value, "manualEntryAllowed")))
						throw OnboardingRuleConfiguration.Invalid("Manual GR entry is not allowed by the applicable numbering rule.");

					sequence = input.ManualSequence ?? 0;
					value = input.ManualValue!.Trim();
				}
				else
				{
					if (configuration != null && OnboardingRuleConfiguration.Text(configuration.Root.GetProperty("grNumber"), "assignmentMode") == "manual")
						throw OnboardingRuleConfiguration.Invalid("Enter a GR number and reason under the manual assignment rule.");
					(sequence, value) = series.Allocate(now);
					await seriesRepository.UpdateAsync(series, false);
				}
				if (await allocationRepository.AnyAsync(a => a.Value == value && a.Status != StudentIdentifierAllocationStatus.Released) ||
					await studentRepository.AnyAsync(s => s.Id != student.Id && s.GrNumber == value))
					throw new BusinessException(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
				var allocation = new StudentIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id, id,
					student.Id, series.Id, series.IdentifierType, sequence, value, now, isManual, input.OverrideReason);
				await allocationRepository.InsertAsync(allocation, false);
				allocationId = allocation.Id;
			}
		}

		onboardingCase.RecordIdentifier(allocationId, input.RollNumber, now);
		await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().RecordIssuedAsync(onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId, onboardingCase.DivisionId!.Value, input.RollNumber);
		await InvalidateApprovalAsync(id, "Identifiers changed; review onboarding again.");
		await CompleteSystemStepAsync(id, StudentOnboardingStepKeys.IdentifiersAndRoll,
			$"Identifiers prepared; roll number {input.RollNumber} reserved for activation.", now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "IdentifiersPrepared", $"Identifier and roll number {input.RollNumber} prepared.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await MapCaseAsync(onboardingCase, true); await rollWork.CompleteAsync(); return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<StudentOnboardingRollPreviewDto> PreviewRollAsync(Guid id, StudentOnboardingRollPreviewInput input)
	{
		var item = await GetCaseAsync(id);
		var divisionId = input.DivisionId ?? item.DivisionId;
		if (!divisionId.HasValue) throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		if (input.DivisionId.HasValue)
		{
			var division = await divisionRepository.FindAsync(d => d.Id == divisionId.Value && d.TenantId == CurrentTenant.Id);
			if (division == null || division.StandardId != item.IntendedStandardId)
				throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentScopeAccessService>()
				.EnsureCanAccessPlacementAsync(item.AcademicYearId, item.IntendedStandardId, divisionId, nameof(PreviewRollAsync));
		}
		var reader = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>();
		var rule = await reader.CaptureForCaseAsync(item, input.DivisionId);
		var proposal = await ProposeCaseRollAsync(item, input.RollNumber, input.RollAssignmentMode, input.Reason, rule, input.DivisionId);
		var existingRoll = divisionId == item.DivisionId || item.IsIdentifierLocked("roll") ? item.RollNumber : null;
		return new()
		{
			DivisionId = divisionId,
			SchoolAssignmentMode = (item.IsIdentifierLocked("roll") || await IsDivisionIdentifierGroupLockedAsync(divisionId, item.AcademicYearId, "roll")) && !existingRoll.HasValue ? "automatic" : await reader.GetAssignmentModeAsync(item.AcademicYearId),
			RollAssignmentMode = proposal.AssignmentMode, ProposedRollNumber = proposal.RollNumber, ConcurrencyStamp = item.ConcurrencyStamp
		};
	}
	private async Task<StudentOnboardingRollProposal> ProposeCaseRollAsync(StudentOnboardingCase item, int? roll, string? mode, string? reason, string rule, Guid? selectedDivisionId = null)
	{
		var divisionId = selectedDivisionId ?? item.DivisionId ?? throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		// Placement save clears an unlocked roll in another division, but preserves a locked identifier.
		var existingRoll = divisionId == item.DivisionId || item.IsIdentifierLocked("roll") ? item.RollNumber : null;
		if (selectedDivisionId.HasValue && selectedDivisionId != item.DivisionId && item.IsIdentifierLocked("roll") &&
			existingRoll.HasValue && roll.HasValue && roll != existingRoll)
			throw new BusinessException("Students:Onboarding:IdentifierLocked");
        if (!existingRoll.HasValue && (item.IsIdentifierLocked("roll") || await IsDivisionIdentifierGroupLockedAsync(divisionId, item.AcademicYearId, "roll")))
        {
            var automaticRule = System.Text.Json.Nodes.JsonNode.Parse(rule) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            automaticRule["assignmentMode"] = "automatic"; automaticRule["startingNumber"] = 1; automaticRule["numberingScope"] = "class-division";
            rule = automaticRule.ToJsonString(); mode = "automatic"; roll = null;
        }
		var selected = StudentOnboardingRollNumberAllocator.ResolveMode(rule, mode);
		if (selected == "automatic" && existingRoll.HasValue)
		{
			await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().ValidateAsync(item.StudentId, item.AcademicYearId, item.IntendedStandardId, divisionId, existingRoll, reason ?? "", rule);
			return new(selected, existingRoll.Value);
		}
		return await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollNumberAllocator>().ProposeAsync(item.StudentId, item.AcademicYearId, item.IntendedStandardId, divisionId, roll, mode, rule, reason ?? "");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingCaseDto> PreviewActivationAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var steps = await stepRepository.GetListAsync(item => item.OnboardingCaseId == id);
		if (await CaptureAutomaticRequirementsAsync(onboardingCase))
		{
			await RefreshProfileReadinessAsync(onboardingCase, steps, Clock.Now, requirementsCaptured: true);
			await CurrentUnitOfWork!.SaveChangesAsync();
		}
		var blockers = await GetActivationBlockersAsync(onboardingCase, steps, DateTime.UtcNow);
		var dto = await MapCaseAsync(onboardingCase, true, steps);
		if (!onboardingCase.StudentEnrollmentId.HasValue &&
		    onboardingCase.Status is not StudentOnboardingStatus.Completed and not StudentOnboardingStatus.Cancelled)
		{
			if (blockers.Count == 0) dto.Status = StudentOnboardingStatus.ReadyForActivation;
			else if (dto.Status == StudentOnboardingStatus.ReadyForActivation) dto.Status = StudentOnboardingStatus.InProgress;
		}
		dto.LastError = blockers.Count == 0 ? null : string.Join("; ", blockers);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate)]
	public async Task<StudentOnboardingCaseDto> ActivateAsync(Guid id, ActivateStudentOnboardingDto input)
	{
        StudentOnboardingCaseDto activated;
		try
		{
			using var activation = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
			activated = await ActivateCoreAsync(id, input);
            await activation.CompleteAsync();
		}
		catch (Exception exception) when (exception is Volo.Abp.Data.AbpDbConcurrencyException ||
			StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			// A competing command owns the committed state; do not mark its case as failed.
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		}
		catch (Exception exception)
		{
			try
			{
				await RecordActivationFailureAsync(id, exception);
			}
			catch (Exception trackingException)
			{
				Logger.LogWarning(trackingException,
					"Could not persist activation failure state for onboarding case {OnboardingCaseId}.", id);
			}
			throw;
		}
        if (!activated.StudentEnrollmentId.HasValue || activated.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled) return activated;
        return await PrepareActivatedAccessAsync(id);
	}

    private async Task<StudentOnboardingCaseDto> PrepareActivatedAccessAsync(Guid id)
    {
        try
        {
            using var preparation = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var item = await GetCaseAsync(id);
            await LinkSiblingParentAsync(item);
            await PrepareAutomaticPortalAccountsAsync(item);
            await QueueAutomaticCredentialEmailsAsync(item);
            await RefreshProvisioningAsync(item);
            await CurrentUnitOfWork!.SaveChangesAsync();
            var result = await MapCaseAsync(item, true);
            await preparation.CompleteAsync();
            return result;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "App access preparation remains pending for activated case {CaseId}.", id);
            // An aborted account transaction must not undo the committed enrollment or poison this repair transaction.
            using var repair = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var item = await GetCaseAsync(id);
            if (item.StudentEnrollmentId.HasValue && item.Status is not (StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled))
            {
                item.RequireAttention("Enrollment is active. Retry app access preparation in the Credentials tab.");
                await caseRepository.UpdateAsync(item, false);
                await AddEventAsync(id, "AccessPending", "Enrollment is active; retry app access preparation.");
            }
            await CurrentUnitOfWork!.SaveChangesAsync();
            var result = await MapCaseAsync(item, true);
            await repair.CompleteAsync();
            return result;
        }
    }

	private async Task<StudentOnboardingCaseDto> ActivateCoreAsync(Guid id, ActivateStudentOnboardingDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		if (onboardingCase.StudentEnrollmentId.HasValue)
		{
			if (input.GuardianAccessSelection != null) EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
			await PrepareSiblingParentSelectionAsync(onboardingCase, input.SiblingParentSelection, input.GuardianAccessSelection);
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Finance.Fees.Collections.IAdmissionFinanceIntegration>()
				.BindActivatedStudentAsync(onboardingCase.ApplicationId, onboardingCase.StudentId, onboardingCase.StudentEnrollmentId.Value);
			await CurrentUnitOfWork!.SaveChangesAsync();
			return await MapCaseAsync(onboardingCase, true);
		}
		var reviewedReturn = ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(onboardingCase).HasValue;
		if (reviewedReturn) await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(onboardingCase);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		await PrepareSiblingParentSelectionAsync(onboardingCase, input.SiblingParentSelection, input.GuardianAccessSelection);
		onboardingCase.BeginActivation(input.IdempotencyKey);
		var now = DateTime.UtcNow;
		var steps = await stepRepository.GetListAsync(item => item.OnboardingCaseId == id);
		await RefreshProfileReadinessAsync(onboardingCase, steps, now);
		var blockers = await GetActivationBlockersAsync(onboardingCase, steps, now);
		if (blockers.Count > 0)
			throw new BusinessException(StudentOnboardingErrorCodes.RequiredStepsIncomplete)
				.WithData("Blockers", string.Join("; ", blockers));

		var finalStep = steps.Single(item => item.Key == StudentOnboardingStepKeys.FinalReadiness);
		if (!IsSatisfied(finalStep.Status))
		{
			finalStep.Complete(CurrentUser.Id, now, "All mandatory onboarding gates passed.");
			await stepRepository.UpdateAsync(finalStep, false);
		}
		onboardingCase.MarkReady(now);

		var reservation = await reservationRepository.FindAsync(onboardingCase.SeatReservationId!.Value) ??
			throw new BusinessException(StudentOnboardingErrorCodes.SeatReservationExpired);
		reservation.Commit(now);
		var allocation = onboardingCase.IdentifierAllocationId.HasValue
			? await allocationRepository.FindAsync(onboardingCase.IdentifierAllocationId.Value)
			: null;
		if (allocation != null) allocation.Commit(now);
		var student = await studentRepository.FindAsync(onboardingCase.StudentId) ??
			throw new EntityNotFoundException(typeof(Student), onboardingCase.StudentId);
		if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue && allocation == null)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
		if (reviewedReturn && (allocation != null || string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue))
			throw new BusinessException("Students:Records:RenewalChecklistRequired");
		if (await enrollmentRepository.AnyAsync(item => item.StudentId == student.Id &&
			item.AcademicYearId == onboardingCase.AcademicYearId && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.EnrollmentAlreadyExists);
		if (await enrollmentRepository.AnyAsync(item => item.AcademicYearId == onboardingCase.AcademicYearId &&
			item.DivisionId == onboardingCase.DivisionId && item.RollNumber == onboardingCase.RollNumber && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);

		var enrollment = reviewedReturn
			? await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ActivateEnrollmentAsync(onboardingCase)
			: StudentEnrollment.Create(GuidGenerator.Create(), CurrentTenant.Id, student.Id, onboardingCase.AcademicYearId,
				onboardingCase.IntendedStandardId, onboardingCase.DivisionId, onboardingCase.RollNumber, onboardingCase.Id);
		if (!reviewedReturn) await enrollmentRepository.InsertAsync(enrollment, false);
		if (allocation != null && !reviewedReturn)
		{
			var legacyGrNo = int.TryParse(allocation.Value, out var parsed) ? parsed : (int?)null;
			student.AssignGrNumber(allocation.Value, legacyGrNo);
			await allocationRepository.UpdateAsync(allocation, false);
		}
		student.UpdateAcademicPlacement(onboardingCase.AcademicYearId, onboardingCase.IntendedBoardId,
			onboardingCase.IntendedMediumId, onboardingCase.IntendedStageId, onboardingCase.IntendedStandardId,
			onboardingCase.DivisionId);
		student.AssignRollNo(onboardingCase.RollNumber);
		if (reviewedReturn) student.ChangeOperationalStatus(StudentStatus.Active, "Reviewed Records return activated through Student Onboarding.");
		else student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		await studentRepository.UpdateAsync(student, false);
		await reservationRepository.UpdateAsync(reservation, false);
		onboardingCase.BeginProvisioning(enrollment.Id);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await EnsureServiceHandoffsAsync(onboardingCase);
		await AddEventAsync(id, "Activated", "Enrollment activated. Complete the requested app access setup.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Finance.Fees.Collections.IAdmissionFinanceIntegration>()
			.BindActivatedStudentAsync(onboardingCase.ApplicationId, onboardingCase.StudentId, enrollment.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
	}

	private async Task RecordActivationFailureAsync(Guid id, Exception exception)
	{
		await CurrentUnitOfWork!.RollbackAsync();
		if (exception is BusinessException businessException &&
		    businessException.Code == StudentOnboardingErrorCodes.ConcurrencyConflict)
			return;

		using var repairUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var onboardingCase = await caseRepository.FindAsync(id);
		if (onboardingCase != null && onboardingCase.Status is not StudentOnboardingStatus.Completed and not StudentOnboardingStatus.Cancelled)
		{
			var reason = GetSafeActivationFailureReason(exception);
			onboardingCase.RequireAttention(reason);
			await caseRepository.UpdateAsync(onboardingCase, false);
			await eventRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyAuditWriter>().CreateOnboardingEvent(id, "ActivationFailed", reason), false);
		}
		await repairUnitOfWork.CompleteAsync();
	}

	private static string GetSafeActivationFailureReason(Exception exception)
	{
		if (exception is BusinessException businessException)
		{
			if (businessException.Data["Blockers"] is string blockers && !string.IsNullOrWhiteSpace(blockers))
				return $"Activation blocked: {blockers}"[..Math.Min(2000, $"Activation blocked: {blockers}".Length)];
			var message = string.IsNullOrWhiteSpace(businessException.Message)
				? businessException.Code ?? "Activation validation failed."
				: businessException.Message;
			return message[..Math.Min(2000, message.Length)];
		}
		return "Activation failed due to an internal error. No operational enrollment changes were committed; use the repair action after the cause is resolved.";
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel)]
	public async Task<StudentOnboardingCaseDto> CancelAsync(Guid id, CancelStudentOnboardingDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		var now = DateTime.UtcNow;
		if (onboardingCase.SeatReservationId.HasValue)
		{
			var reservation = await reservationRepository.FindAsync(onboardingCase.SeatReservationId.Value);
			if (reservation != null)
			{
				reservation.Release("Onboarding cancelled", now);
				await reservationRepository.UpdateAsync(reservation, false);
			}
		}
		if (onboardingCase.IdentifierAllocationId.HasValue)
		{
			var allocation = await allocationRepository.FindAsync(onboardingCase.IdentifierAllocationId.Value);
			if (allocation != null)
			{
				allocation.Release();
				await allocationRepository.UpdateAsync(allocation, false);
			}
		}
		onboardingCase.Cancel(input.Reason, now);
		await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnClosureService>()
			.CloseCancelledOnboardingAsync(onboardingCase);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "Cancelled", $"Onboarding cancelled: {input.Reason.Trim()}");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentOnboardingServiceHandoffDto>> GetServiceHandoffsAsync(Guid id)
	{
		await GetCaseAsync(id);
		return (await handoffRepository.GetListAsync(item => item.OnboardingCaseId == id))
			.OrderBy(item => item.ServiceKey).Select(MapHandoff).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public async Task<StudentOnboardingServiceHandoffDto> RetryServiceHandoffAsync(Guid id, Guid handoffId)
	{
		var item = await GetCaseAsync(id);
		var handoff = await handoffRepository.FindAsync(item => item.Id == handoffId && item.OnboardingCaseId == id) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingServiceHandoff), handoffId);
		if (handoff.ServiceKey is not ("finance-first-payment" or "student-account" or "guardian-account") ||
			!item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		await RefreshProvisioningAsync(item);
		return MapHandoff(handoff);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentOnboardingPolicyDto>> GetPoliciesAsync(Guid academicYearId)
	{
		var standardIds = await GetReadableSettingsStandardIdsAsync(academicYearId, nameof(GetPoliciesAsync));
		var query = (await policyRepository.GetQueryableAsync()).Where(item => item.AcademicYearId == academicYearId);
		if (standardIds != null)
			query = query.Where(item => !item.StandardId.HasValue || Enumerable.Contains(standardIds, item.StandardId.Value));
		var policies = await AsyncExecuter.ToListAsync(query);
		return policies.OrderByDescending(item => item.OfferingId.HasValue).ThenByDescending(item => item.StandardId.HasValue)
			.Select(MapPolicy).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentOnboardingPolicyDto> CreatePolicyAsync(UpsertStudentOnboardingPolicyDto input)
	{
		await ValidatePolicyScopeAsync(input);
		if (await policyRepository.AnyAsync(item => item.AcademicYearId == input.AcademicYearId &&
			item.OfferingId == input.OfferingId && item.StandardId == input.StandardId))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		var policy = new StudentOnboardingPolicy(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			input.OfferingId, input.StandardId, input.Name, input.RequireDocuments, input.RequireFamilyDetails,
			input.RequireCommunicationDetails, input.RequireMedicalDetails, input.RequireFeeClearance,
			input.RequireStudentAccount, input.RequireGuardianAccount, input.RequireServiceConsent,
			input.SeatReservationMinutes, input.SlaHours);
		await policyRepository.InsertAsync(policy, true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentOnboardingPolicyDto> UpdatePolicyAsync(Guid id, UpsertStudentOnboardingPolicyDto input)
	{
		var policy = await policyRepository.FindAsync(id) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingPolicy), id);
		await EnsureSettingsTargetScopeAsync(policy.AcademicYearId, policy.StandardId, nameof(UpdatePolicyAsync));
		EnsureConcurrency(policy, input.ConcurrencyStamp);
		await ValidatePolicyScopeAsync(input);
		if (policy.AcademicYearId != input.AcademicYearId || policy.OfferingId != input.OfferingId ||
		    policy.StandardId != input.StandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict)
				.WithData("Reason", "Policy scope is immutable; create a new override instead.");
		policy.Update(input.Name, input.RequireDocuments, input.RequireFamilyDetails,
			input.RequireCommunicationDetails, input.RequireMedicalDetails, input.RequireFeeClearance,
			input.RequireStudentAccount, input.RequireGuardianAccount, input.RequireServiceConsent,
			input.SeatReservationMinutes, input.SlaHours, input.IsActive);
		await policyRepository.UpdateAsync(policy, true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentOnboardingSettingsDto>> GetSettingsAsync(Guid? academicYearId)
	{
		// School defaults are inherited by every scoped workspace; a selected year's override is scoped data.
		if (academicYearId.HasValue)
			await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>()
				.EnsureCanAccessAcademicYearAsync(academicYearId.Value, nameof(GetSettingsAsync));
		var settings = await settingsRepository.GetListAsync(item =>
			!item.AcademicYearId.HasValue || academicYearId.HasValue && item.AcademicYearId == academicYearId);
		return settings.OrderBy(item => item.AcademicYearId.HasValue).Select(MapSettings).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentOnboardingSettingsDto> SaveSettingsAsync(SaveStudentOnboardingSettingsDto input)
	{
		await EnsureSettingsScopeAsync(input.AcademicYearId);
		await configurationManager.ValidateAsync(input.ConfigurationJson, input.AcademicYearId);
		if (input.AcademicYearId.HasValue &&
		    !await academicYearRepository.AnyAsync(item => item.Id == input.AcademicYearId.Value))
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);

		var settings = await settingsRepository.FindAsync(item => item.AcademicYearId == input.AcademicYearId);
		if (settings == null)
		{
			if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp))
				throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
            var inherited = input.AcademicYearId.HasValue
                ? await settingsRepository.FindAsync(item => !item.AcademicYearId.HasValue) : null;
            if (inherited != null)
                settings = StudentOnboardingSettings.CreateAcademicYearOverride(GuidGenerator.Create(), inherited,
                    input.AcademicYearId!.Value, input.SchemaVersion, input.ConfigurationJson);
            else
            {
                settings = new StudentOnboardingSettings(GuidGenerator.Create(), CurrentTenant.Id,
                    input.AcademicYearId, input.SchemaVersion, "{}");
                settings.Update(input.SchemaVersion, input.ConfigurationJson);
            }
			await settingsRepository.InsertAsync(settings, true);
		}
		else
		{
			// PUT is idempotent: a retried/duplicate request may carry the previous stamp after
			// an identical request has already committed. Returning the current representation is
			// safe, while a stale request that changes configuration must still be rejected below.
			if (!string.Equals(input.ConcurrencyStamp, settings.ConcurrencyStamp, StringComparison.Ordinal) &&
			    settings.MatchesConfiguration(input.SchemaVersion, input.ConfigurationJson))
				return MapSettings(settings);
			EnsureConcurrency(settings, input.ConcurrencyStamp);
			settings.Update(input.SchemaVersion, input.ConfigurationJson);
			await settingsRepository.UpdateAsync(settings, true);
		}

		return MapSettings(settings);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Delete)]
	public async Task DeleteSettingsAsync(Guid id, string? concurrencyStamp)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Delete);
		var settings = await settingsRepository.FindAsync(id) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingSettings), id);
		await EnsureSettingsScopeAsync(settings.AcademicYearId);
		EnsureConcurrency(settings, concurrencyStamp);
		await settingsRepository.DeleteAsync(settings, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetIdentifierSeriesAsync(Guid academicYearId)
	{
		var standardIds = await GetReadableSettingsStandardIdsAsync(academicYearId, nameof(GetIdentifierSeriesAsync));
		var query = (await seriesRepository.GetQueryableAsync()).Where(item => item.AcademicYearId == academicYearId);
		if (standardIds != null)
			query = query.Where(item => !item.StandardId.HasValue || Enumerable.Contains(standardIds, item.StandardId.Value));
		var series = await AsyncExecuter.ToListAsync(query);
		return series.OrderBy(item => item.Code).Select(MapSeries).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentIdentifierSeriesDto> CreateIdentifierSeriesAsync(CreateStudentIdentifierSeriesDto input)
	{
		if (!await academicYearRepository.AnyAsync(item => item.Id == input.AcademicYearId) ||
		    input.StandardId.HasValue && !await standardRepository.AnyAsync(item => item.Id == input.StandardId.Value))
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		await EnsureSettingsTargetScopeAsync(input.AcademicYearId, input.StandardId, nameof(CreateIdentifierSeriesAsync));
		var normalizedCode = input.Code.Trim().ToUpperInvariant();
		if (await seriesRepository.AnyAsync(item => item.Code == normalizedCode))
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesAlreadyExists);
		var series = new StudentIdentifierSeries(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId,
			input.StandardId, input.IdentifierType, input.Code, input.Name, input.Prefix, input.Suffix,
			input.Padding, input.StartingSequence, input.MaximumSequence, input.EffectiveFrom, input.EffectiveUntil);
		await seriesRepository.InsertAsync(series, true);
		return MapSeries(series);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentIdentifierSeriesDto> DeactivateIdentifierSeriesAsync(Guid id, string? concurrencyStamp)
	{
		var series = await seriesRepository.FindAsync(id) ??
			throw new EntityNotFoundException(typeof(StudentIdentifierSeries), id);
		await EnsureSettingsTargetScopeAsync(series.AcademicYearId, series.StandardId, nameof(DeactivateIdentifierSeriesAsync));
		if (string.IsNullOrWhiteSpace(concurrencyStamp) ||
		    !string.Equals(concurrencyStamp, series.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		series.Deactivate();
		await seriesRepository.UpdateAsync(series, true);
		return MapSeries(series);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public Task<HistoricalAdmissionReconciliationDto> GetHistoricalReconciliationAsync() =>
		ReconcileHistoricalAdmissionsAsync(apply: false);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public Task<HistoricalAdmissionReconciliationDto> BackfillHistoricalAdmissionsAsync() =>
		ReconcileHistoricalAdmissionsAsync(apply: true);

	private async Task<HistoricalAdmissionReconciliationDto> ReconcileHistoricalAdmissionsAsync(bool apply)
	{
		var applicationQuery = await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationScopeAccessService>()
			.GetScopedApplicationsAsync();
		var applications = await AsyncExecuter.ToListAsync(applicationQuery.Where(item => item.Status == AdmissionApplicationStatus.Enrolled));
		var result = new HistoricalAdmissionReconciliationDto
		{
			TotalHistoricalApplications = applications.Count
		};

		foreach (var application in applications.OrderBy(item => item.CreationTime))
		{
			var existingCase = await caseRepository.FindAsync(item => item.ApplicationId == application.Id);
			if (existingCase != null && application.OnboardingCaseId == existingCase.Id)
			{
				result.AlreadyLinked++;
				continue;
			}

			var studentId = application.StudentId ?? application.EnrolledStudentId;
			if (!studentId.HasValue || !application.StudentEnrollmentId.HasValue)
			{
				AddReconciliationIssue(result, application, "Missing Student or StudentEnrollment link.");
				continue;
			}
			var student = await studentRepository.FindAsync(studentId.Value);
			var enrollment = await enrollmentRepository.FindAsync(application.StudentEnrollmentId.Value);
			if (student == null || enrollment == null || enrollment.StudentId != student.Id)
			{
				AddReconciliationIssue(result, application, "Student and StudentEnrollment links are inconsistent.");
				continue;
			}
			if (existingCase != null || application.OnboardingCaseId.HasValue ||
			    enrollment.OnboardingCaseId.HasValue)
			{
				AddReconciliationIssue(result, application, "A partial or conflicting onboarding link already exists.");
				continue;
			}

			var offering = await offeringRepository.FindAsync(application.AdmissionOfferingId);
			var cycle = offering == null ? null : await cycleRepository.FindAsync(offering.AdmissionCycleId);
			if (offering == null || cycle == null || enrollment.AcademicYearId != cycle.AcademicYearId ||
			    enrollment.StandardId.HasValue && enrollment.StandardId != offering.StandardId)
			{
				AddReconciliationIssue(result, application, "Offering, academic year, or standard does not match the enrollment.");
				continue;
			}

			result.ReadyToBackfill++;
			if (!apply) continue;
			var completedAt = application.DecisionAt ?? enrollment.CreationTime;
			var onboardingCase = new StudentOnboardingCase(GuidGenerator.Create(), CurrentTenant.Id,
				application.Id, application.ApplicantId, student.Id, offering.Id, cycle.AcademicYearId,
				offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
				$"historical:{application.Id:N}", null, completedAt);
			onboardingCase.SetCampusId(offering.CampusId);
			onboardingCase.CompleteHistoricalImport(enrollment.Id, enrollment.DivisionId,
				enrollment.RollNumber ?? student.RollNo, completedAt);
			await caseRepository.InsertAsync(onboardingCase, false);
			for (var index = 0; index < StudentOnboardingStepKeys.Defaults.Length; index++)
			{
				var definition = StudentOnboardingStepKeys.Defaults[index];
				await stepRepository.InsertAsync(new StudentOnboardingStep(GuidGenerator.Create(), CurrentTenant.Id,
					onboardingCase.Id, definition.Key, definition.Label, index + 1, false,
					StudentOnboardingStepStatus.NotRequired), false);
			}
			application.LinkHistoricalOnboarding(student.Id, onboardingCase.Id);
			enrollment.LinkOnboardingCase(onboardingCase.Id);
			await applicationRepository.UpdateAsync(application, false);
			await enrollmentRepository.UpdateAsync(enrollment, false);
			await eventRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyAuditWriter>().CreateOnboardingEvent(onboardingCase.Id, "HistoricalBackfill",
				"Imported from a verified legacy application and StudentEnrollment link; historical checklist evidence was not fabricated."), false);
			result.Backfilled++;
		}

		result.Invalid = result.Issues.Count;
		if (apply && result.Backfilled > 0) await CurrentUnitOfWork!.SaveChangesAsync();
		return result;
	}

	private static void AddReconciliationIssue(HistoricalAdmissionReconciliationDto result,
		AdmissionApplication application, string reason) =>
		result.Issues.Add(new HistoricalAdmissionReconciliationIssueDto
		{
			ApplicationId = application.Id,
			ApplicationNumber = application.ApplicationNumber,
			Reason = reason
		});

	private async Task ValidatePolicyScopeAsync(UpsertStudentOnboardingPolicyDto input)
	{
		if (!await academicYearRepository.AnyAsync(item => item.Id == input.AcademicYearId))
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		if (input.OfferingId.HasValue != input.StandardId.HasValue)
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid)
				.WithData("Reason", "An override must identify both an offering and its standard.");
		if (!input.OfferingId.HasValue)
		{
			await EnsureSettingsTargetScopeAsync(input.AcademicYearId, null, nameof(ValidatePolicyScopeAsync));
			return;
		}
		var offering = await offeringRepository.FindAsync(input.OfferingId.Value);
		if (offering == null || offering.StandardId != input.StandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		var cycle = await cycleRepository.FindAsync(offering.AdmissionCycleId);
		if (cycle == null || cycle.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		await EnsureSettingsTargetScopeAsync(input.AcademicYearId, input.StandardId, nameof(ValidatePolicyScopeAsync));
	}

	private async Task<List<string>> GetActivationBlockersAsync(
		StudentOnboardingCase onboardingCase, IReadOnlyCollection<StudentOnboardingStep> steps, DateTime now)
	{
		var feeConfirmed = await admissionFeeSelectionReader.IsConfirmedAsync(await applicationRepository.GetAsync(onboardingCase.ApplicationId));
		// Use the same current Finance evidence as the displayed checklist. Preview must
		// not rewrite saved verification evidence; activation refreshes the stored steps.
		steps = ProjectFeeReadiness(steps, feeConfirmed);
		var blockers = steps.Where(step => step.Key != StudentOnboardingStepKeys.FinalReadiness &&
			step.Required && step.Status != StudentOnboardingStepStatus.Completed).Select(step => step.Label).ToList();
		if (!onboardingCase.DivisionId.HasValue || !onboardingCase.SeatReservationId.HasValue)
			blockers.Add("Academic placement and a live seat reservation are required.");
		else
		{
			var reservation = await reservationRepository.FindAsync(onboardingCase.SeatReservationId.Value);
			if (reservation == null || !reservation.IsLive(now)) blockers.Add("The seat reservation has expired.");
		}
		if (!onboardingCase.RollNumber.HasValue) blockers.Add("A roll number is required.");
		if (!feeConfirmed) blockers.Add("A confirmed fee structure is required.");
		var student = await studentRepository.FindAsync(onboardingCase.StudentId);
		if (student != null && string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue &&
		    !onboardingCase.IdentifierAllocationId.HasValue)
			blockers.Add("A GR/admission identifier is required.");
		return blockers;
	}

	private async Task CompleteSystemStepAsync(Guid caseId, string stepKey, string detail, DateTime at)
	{
		var step = await stepRepository.FindAsync(item => item.OnboardingCaseId == caseId && item.Key == stepKey) ??
			throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
		step.Complete(CurrentUser.Id, at, detail);
		await stepRepository.UpdateAsync(step, false);
	}

	private async Task ResetSystemStepAsync(Guid caseId, string stepKey, string detail)
	{
		var step = await stepRepository.FindAsync(item => item.OnboardingCaseId == caseId && item.Key == stepKey) ??
			throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
		step.Reset(detail);
		await stepRepository.UpdateAsync(step, false);
	}

	private async Task RefreshProfileReadinessAsync(StudentOnboardingCase onboardingCase,
		List<StudentOnboardingStep> steps, DateTime at, bool requirementsCaptured = false)
	{
		if (!requirementsCaptured)
			await CaptureAutomaticRequirementsAsync(onboardingCase);
		var reviewedReturn = ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(onboardingCase).HasValue;
		if (reviewedReturn) await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(onboardingCase, !onboardingCase.StudentEnrollmentId.HasValue);
		var student = await studentRepository.GetWithDetailsAsync(onboardingCase.StudentId) ??
			throw new EntityNotFoundException(typeof(Student), onboardingCase.StudentId);
		await RefreshIdentifierStepAsync(onboardingCase);
		var documentStatus = await studentDocumentManager.GetRequiredDocumentsStatusAsync(student.Id);
		var acceptedAdmissionDocumentTypes = await admissionEvidenceReader.GetAcceptedUploadDocumentTypeIdsAsync(student.Id);
		var documentsReady = documentStatus.Where(item => item.documentType.IsRequired)
			.All(item => item.uploadedDocument?.ValidatedAt.HasValue == true ||
				acceptedAdmissionDocumentTypes.Contains(item.documentType.Id));
		if (onboardingCase.ExtraProperties.ContainsKey("DocumentPoliciesSnapshot"))
			documentsReady = await LazyServiceProvider.LazyGetRequiredService<OnboardingRequirementSnapshotService>()
				.DocumentsReadyAsync(onboardingCase, await applicationRepository.GetAsync(onboardingCase.ApplicationId));
		if (onboardingCase.ExtraProperties.ContainsKey("ReadinessRulesSnapshot"))
			foreach (var step in steps.Where(s => s.Key is StudentOnboardingStepKeys.Identity or StudentOnboardingStepKeys.Family or StudentOnboardingStepKeys.Communication or StudentOnboardingStepKeys.MedicalAndPreviousSchool)) step.SetRequired(false);
		// Admission verifies the applicant, but a returning/reused Student may still have
		// incomplete operational identity. Keep that automatic check mandatory for pending cases.
		if (AutomaticOnboardingReadinessRules.CanAdopt(onboardingCase))
			steps.Single(s => s.Key == StudentOnboardingStepKeys.Identity).SetRequired(true);
        var savedRules = await ReadSavedRulesAsync(onboardingCase);
		if (savedRules.HasValue)
		{
			var rules = savedRules.Value;
			var documentRule = (rules.TryGetProperty("checks", out var checks) && checks.ValueKind == System.Text.Json.JsonValueKind.Array
				? checks.EnumerateArray().AsEnumerable() : [])
				.FirstOrDefault(check => OnboardingRuleConfiguration.Text(check, "key") == "documents");
			var requireDocuments = OnboardingRuleConfiguration.Boolean(rules, "requireDocuments", true) &&
				(documentRule.ValueKind == System.Text.Json.JsonValueKind.Undefined || OnboardingRuleConfiguration.Boolean(documentRule, "required", true));
			steps.Single(s => s.Key == StudentOnboardingStepKeys.Documents).SetRequired(true);
			var allocation = onboardingCase.IdentifierAllocationId.HasValue
				? await allocationRepository.FindAsync(onboardingCase.IdentifierAllocationId.Value) : null;
			var needsApproval = OnboardingRuleConfiguration.Text(rules, "approvalMode") == "always" ||
				OnboardingRuleConfiguration.Text(rules, "approvalMode") == "conditional" && allocation?.IsManual == true &&
				OnboardingRuleConfiguration.Strings(rules, "approvalTriggers").Contains("manual-identifier");
			needsApproval |= OnboardingRuleConfiguration.Text(rules, "approvalMode") == "conditional" &&
				onboardingCase.GetAdmissionRoute() == "Mid-year admission" &&
				OnboardingRuleConfiguration.Strings(rules, "approvalTriggers").Contains("mid-year-admission");
			needsApproval |= OnboardingRuleConfiguration.Text(rules, "approvalMode") == "conditional" &&
				OnboardingRuleConfiguration.Strings(rules, "approvalTriggers").Contains("incomplete-optional-check") &&
				!requireDocuments && !documentsReady;
			var placementChanged = onboardingCase.ExtraProperties.TryGetValue("PlacementChanged", out var changed) &&
				string.Equals(changed?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
			needsApproval |= placementChanged && (OnboardingRuleConfiguration.Text(rules, "approvalMode") == "conditional" && OnboardingRuleConfiguration.Strings(rules, "approvalTriggers").Contains("division-change"));
			var approval = steps.SingleOrDefault(s => s.Key == StudentOnboardingStepKeys.ApprovalReview);
			if (reviewedReturn) needsApproval = false;
			if (approval == null && needsApproval)
			{
				approval = new StudentOnboardingStep(GuidGenerator.Create(), CurrentTenant.Id, onboardingCase.Id,
					StudentOnboardingStepKeys.ApprovalReview, "Onboarding approval", 85, true);
				await stepRepository.InsertAsync(approval, false);
				steps.Add(approval);
			}
			if (approval != null)
			{
				approval.SetRequired(needsApproval);
				if (needsApproval && IsSatisfied(approval.Status) &&
				    (!onboardingCase.ExtraProperties.TryGetValue("ApprovalConfigurationHash", out var hash) || hash?.ToString() != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rules.GetRawText())))))
					approval.Reset("Settings changed since approval. Review the current requirements.");
				await stepRepository.UpdateAsync(approval, false);
			}
		}
		// Applies to legacy policies as well: these dependencies require an enrollment first.
		if (reviewedReturn && steps.SingleOrDefault(s => s.Key == StudentOnboardingStepKeys.ApprovalReview) is { } returnApproval)
		{
			returnApproval.SetRequired(false); await stepRepository.UpdateAsync(returnApproval, false);
		}
		var serviceStep = steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices);
		serviceStep.SetRequired(true);
		var feeApplication = await applicationRepository.GetAsync(onboardingCase.ApplicationId);
		if (await admissionFeeSelectionReader.IsConfirmedAsync(feeApplication)) serviceStep.Complete(null, at,
            feeApplication.GetFeeSelection()?.OnboardingConfirmedAt == null ? "Confirmed and fully paid during Admissions." : "Fee structure confirmed. Payment is handled separately by Finance.");
		else serviceStep.Block("Select and confirm a published fee structure in the Fees tab.");
		await stepRepository.UpdateAsync(serviceStep, false);
		SetComputedStep(steps, StudentOnboardingStepKeys.Identity,
			AutomaticOnboardingReadinessRules.HasCompleteIdentity(student),
			"Student name, date of birth, and gender are complete.",
			"Complete the Student name, date of birth, and gender.", at);
		SetComputedStep(steps, StudentOnboardingStepKeys.Family,
			student.FamilyDetail != null && (student.FamilyDetail.FatherDetails != null ||
				student.FamilyDetail.MotherDetails != null || student.FamilyDetail.GuardianDetails != null),
			"At least one family or guardian record is available.",
			"Add at least one parent or guardian record.", at);
		SetComputedStep(steps, StudentOnboardingStepKeys.Communication,
			student.ContactDetail != null && !string.IsNullOrWhiteSpace(student.ContactDetail.PrimaryContactNo) &&
			(!string.IsNullOrWhiteSpace(student.ContactDetail.Address1) || student.CommunicationDetail?.LocalAddress != null),
			"Primary contact and address information are available.",
			"Add a primary contact number and local address.", at);
		SetComputedStep(steps, StudentOnboardingStepKeys.Documents, documentsReady,
			"All applicable Offering documents have accepted verification evidence.",
			"Collect and verify each pending document required by this Offering.", at);
		SetComputedStep(steps, StudentOnboardingStepKeys.MedicalAndPreviousSchool,
			student.MedicalRecord != null || student.PreviousSchoolDetail != null,
			"Medical or previous-school information is available.",
			"Add the required medical or previous-school information.", at);

		foreach (var step in steps.Where(step => IsProfileReadinessStep(step.Key)))
			await stepRepository.UpdateAsync(step, false);
	}

	private static void SetComputedStep(IReadOnlyCollection<StudentOnboardingStep> steps, string key,
		bool ready, string readyDetail, string blockedDetail, DateTime at)
	{
		var step = steps.Single(item => item.Key == key);
		if (!step.Required) return;
		if (ready) step.Complete(null, at, readyDetail);
		else step.Block(blockedDetail);
	}

	private static bool IsProfileReadinessStep(string key) => key is
		StudentOnboardingStepKeys.Identity or
		StudentOnboardingStepKeys.Family or
		StudentOnboardingStepKeys.Communication or
		StudentOnboardingStepKeys.Documents or
		StudentOnboardingStepKeys.MedicalAndPreviousSchool;

	private async Task EnsureServiceHandoffsAsync(StudentOnboardingCase onboardingCase)
	{
		var policy = await ResolvePolicyAsync(onboardingCase);
		var configuration = await configurationManager.GetAsync(onboardingCase.AcademicYearId);
		var rules = await ReadSavedRulesAsync(onboardingCase);
		// Optional services need an explicit family/staff selection. Until that selection is stored,
		// do not fabricate transport, hostel, meal, uniform, device, library, or health requests.
		var definitions = new List<(string Key, bool Required)>();
		if (rules.HasValue ? OnboardingRuleConfiguration.Boolean(rules.Value, "studentAccount") : policy?.RequireStudentAccount == true)
			definitions.Add(("student-account", true));
		if (rules.HasValue ? OnboardingRuleConfiguration.Boolean(rules.Value, "guardianAccount") : policy?.RequireGuardianAccount == true)
			definitions.Add(("guardian-account", true));
		if (!onboardingCase.ExtraProperties.ContainsKey("ReadinessRulesSnapshot") && policy?.RequireServiceConsent == true) definitions.Add(("service-consent", true));
		var existingKeys = (await handoffRepository.GetListAsync(item => item.OnboardingCaseId == onboardingCase.Id))
			.Select(item => item.ServiceKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var definition in definitions.Where(item => !existingKeys.Contains(item.Key)))
		{
			await handoffRepository.InsertAsync(new StudentOnboardingServiceHandoff(GuidGenerator.Create(),
				CurrentTenant.Id, onboardingCase.Id, definition.Key, definition.Required,
				$"{onboardingCase.Id:N}:{definition.Key}"), false);
		}
	}

	private async Task<StudentOnboardingPolicy?> ResolvePolicyAsync(StudentOnboardingCase onboardingCase)
	{
		var policies = await policyRepository.GetListAsync(item =>
			item.AcademicYearId == onboardingCase.AcademicYearId && item.IsActive);
		return policies
			.Where(item => (item.OfferingId == onboardingCase.OfferingId || !item.OfferingId.HasValue) &&
				(item.StandardId == onboardingCase.IntendedStandardId || !item.StandardId.HasValue))
			.OrderByDescending(item => item.OfferingId == onboardingCase.OfferingId &&
				item.StandardId == onboardingCase.IntendedStandardId)
			.ThenByDescending(item => item.OfferingId == onboardingCase.OfferingId)
			.ThenByDescending(item => item.StandardId == onboardingCase.IntendedStandardId)
			.FirstOrDefault();
	}

	private async Task<StudentOnboardingCaseDto> MapCaseAsync(
		StudentOnboardingCase onboardingCase, bool includeEvents,
		IReadOnlyCollection<StudentOnboardingStep>? loadedSteps = null,
		IReadOnlyDictionary<Guid, OnboardingRuleConfiguration?>? configurations = null)
	{
		OnboardingRuleConfiguration? accountConfiguration = null;
		if (!onboardingCase.StudentEnrollmentId.HasValue)
			accountConfiguration = configurations != null && configurations.TryGetValue(onboardingCase.AcademicYearId, out var configured)
				? configured : await configurationManager.GetAsync(onboardingCase.AcademicYearId);
		if (loadedSteps == null && await CaptureAutomaticRequirementsAsync(onboardingCase, accountConfiguration))
		{
			var currentSteps = await stepRepository.GetListAsync(step => step.OnboardingCaseId == onboardingCase.Id);
			await RefreshProfileReadinessAsync(onboardingCase, currentSteps, Clock.Now, requirementsCaptured: true);
			await CurrentUnitOfWork!.SaveChangesAsync();
			loadedSteps = currentSteps;
		}
		var application = await applicationRepository.FindAsync(onboardingCase.ApplicationId);
		var offering = await offeringRepository.FindAsync(onboardingCase.OfferingId);
		if (!onboardingCase.HasCampusSnapshot) onboardingCase.UseLegacyCampusId(offering?.CampusId);
		var academicYear = await academicYearRepository.FindAsync(onboardingCase.AcademicYearId);
		var board = await boardRepository.FindAsync(onboardingCase.IntendedBoardId);
		var medium = await mediumRepository.FindAsync(onboardingCase.IntendedMediumId);
		var standard = await standardRepository.FindAsync(onboardingCase.IntendedStandardId);
		var division = onboardingCase.DivisionId.HasValue
			? await divisionRepository.FindAsync(onboardingCase.DivisionId.Value)
			: null;
		var student = includeEvents
			? await studentRepository.GetWithDetailsAsync(onboardingCase.StudentId)
			: await studentRepository.FindAsync(onboardingCase.StudentId);
		var owner = onboardingCase.AssignedToUserId.HasValue
			? await identityUserRepository.FindAsync(onboardingCase.AssignedToUserId.Value)
			: null;
		var steps = loadedSteps ?? await stepRepository.GetListAsync(item => item.OnboardingCaseId == onboardingCase.Id);
		var events = includeEvents
			? await eventRepository.GetListAsync(item => item.OnboardingCaseId == onboardingCase.Id)
			: [];
		var actorNames = await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyActorReader>().ReadAsync(events.Select(item => item.CreatorId).Concat(steps.Select(item => item.CompletedByUserId)));
		var feeSelection = application?.GetFeeSelection();
		var feeConfirmed = application != null && await admissionFeeSelectionReader.IsConfirmedAsync(application);
		// Readiness is derived from current evidence even for existing cases, without
		// mutating tracked steps or creating a duplicate confirmation merely on view.
		steps = ProjectFeeReadiness(steps, feeConfirmed);
		var reservedIdentifier = string.IsNullOrWhiteSpace(student?.GrNumber) && student?.GrNo == null
			? await allocationRepository.FindAsync(a => a.OnboardingCaseId == onboardingCase.Id &&
				a.Status == StudentIdentifierAllocationStatus.Reserved)
			: null;
		var handoffs = onboardingCase.StudentEnrollmentId.HasValue
			? await handoffRepository.GetListAsync(h => h.OnboardingCaseId == onboardingCase.Id && h.Required && h.ServiceKey != "finance-first-payment" && (h.ServiceKey != "service-consent" || !onboardingCase.ExtraProperties.ContainsKey("ReadinessRulesSnapshot")))
			: [];
		var requiredHandoffs = onboardingCase.Status != StudentOnboardingStatus.Completed ? handoffs : [];
		var studentAccount = handoffs.Find(h => h.ServiceKey == "student-account");
		var guardianAccount = handoffs.Find(h => h.ServiceKey == "guardian-account");
		var accountPolicy = !onboardingCase.StudentEnrollmentId.HasValue ? await ResolvePolicyAsync(onboardingCase) : null;
		var accountRules = await ReadSavedRulesAsync(onboardingCase);
		var satisfied = steps.Count(step => IsSatisfied(step.Status));
		var primaryBlocker = StudentOnboardingWorkSummary.PrimaryBlocker(steps, requiredHandoffs);
		var progressTotal = steps.Count + requiredHandoffs.Count;
		satisfied += requiredHandoffs.Count(h => h.Status == StudentOnboardingServiceHandoffStatus.Acknowledged);
		return new StudentOnboardingCaseDto
		{
			CapturedStudentFields = includeEvents && student != null
				? AdmissionAdditionalStudentFieldCatalog.ReadStudent(student)
					.Select(field => new CapturedStudentFieldDto
					{
						Key = field.Key, Label = field.Label, Value = field.Value
					}).ToList()
				: [],
			Id = onboardingCase.Id,
			CredentialsPending = await CredentialsPendingAsync(onboardingCase),
			PortalAccountOwners = includeEvents && student != null ? MapPortalAccountOwners(student) : null,
			AdmissionRoute = onboardingCase.GetAdmissionRoute(),
			FeeAssignmentConfirmed = feeConfirmed,
			FirstPaymentConfirmed = null,
			FinanceReadinessDetail = feeConfirmed ? "Fee structure confirmed." : "Confirm a published fee structure.",
			SelectedFeeStructureId = feeSelection?.StructureId,
			FeeStructureConfirmed = feeConfirmed,
			GrNumberLocked = onboardingCase.IsIdentifierLocked("gr") || student?.IsIdentifierLocked("gr") == true || await IsDivisionIdentifierGroupLockedAsync(onboardingCase.DivisionId, onboardingCase.AcademicYearId, "gr"),
			RollNumberLocked = onboardingCase.IsIdentifierLocked("roll") || student?.IsIdentifierLocked("roll") == true || await IsDivisionIdentifierGroupLockedAsync(onboardingCase.DivisionId, onboardingCase.AcademicYearId, "roll"),
			RollChangeReasonRequired = accountConfiguration?.ResolveRule("rollNumber", onboardingCase, Clock.Now) is { } rollSettings && OnboardingRuleConfiguration.Boolean(rollSettings, "requireChangeReason"),
			GrManualEntryAllowed = accountConfiguration == null || accountConfiguration.ResolveRule("grNumber", onboardingCase, Clock.Now) is { } grSettings && OnboardingRuleConfiguration.Boolean(grSettings, "manualEntryAllowed"),
			HasDocumentRequirementsSnapshot = onboardingCase.ExtraProperties.ContainsKey("DocumentPoliciesSnapshot"),
			AdmissionConfirmed = application?.Status == AdmissionApplicationStatus.AdmissionFinalized,
			RequirementsNeedReview = false,
			StudentAccountRequired = onboardingCase.StudentEnrollmentId.HasValue ? studentAccount != null
				: accountRules.HasValue ? OnboardingRuleConfiguration.Boolean(accountRules.Value, "studentAccount") : accountPolicy?.RequireStudentAccount == true,
			GuardianAccountRequired = onboardingCase.StudentEnrollmentId.HasValue ? guardianAccount != null
				: accountRules.HasValue ? OnboardingRuleConfiguration.Boolean(accountRules.Value, "guardianAccount") : accountPolicy?.RequireGuardianAccount == true,
			StudentAccountStatus = studentAccount?.Status,
			GuardianAccountStatus = guardianAccount?.Status,
			ConcurrencyStamp = onboardingCase.ConcurrencyStamp,
			ApplicationId = onboardingCase.ApplicationId,
			ApplicationNumber = application?.ApplicationNumber ?? string.Empty,
			ApplicantId = onboardingCase.ApplicantId,
			StudentId = onboardingCase.StudentId,
			StudentName = student == null ? string.Empty : string.Join(" ",
				new[] { student.FirstName, student.MiddleName, student.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
			OfferingId = onboardingCase.OfferingId,
			OfferingCode = offering?.Code ?? string.Empty,
			CampusId = onboardingCase.GetCampusId(),
			AcademicYearId = onboardingCase.AcademicYearId,
			AcademicYearName = academicYear?.Name ?? string.Empty,
			IntendedBoardId = onboardingCase.IntendedBoardId,
			IntendedBoardName = board?.Type.ToString() ?? string.Empty,
			IntendedMediumId = onboardingCase.IntendedMediumId,
			IntendedMediumName = medium?.Name ?? string.Empty,
			IntendedStageId = onboardingCase.IntendedStageId,
			IntendedStandardId = onboardingCase.IntendedStandardId,
			IntendedStandardName = standard?.Name ?? string.Empty,
			Status = onboardingCase.Status,
			AssignedToUserId = null,
			AssignedToUserName = null,
			DueAt = onboardingCase.DueAt,
			IsHistoricalBackfill = onboardingCase.IsHistoricalBackfill,
			DivisionId = onboardingCase.DivisionId,
			DivisionName = division?.Name,
			SeatReservationId = onboardingCase.SeatReservationId,
			GrNumber = student?.GrNumber ?? student?.GrNo?.ToString() ?? reservedIdentifier?.Value,
			RollNumber = onboardingCase.RollNumber,
			RollAssignmentMode = await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().GetAssignmentModeAsync(onboardingCase.AcademicYearId),
			StudentEnrollmentId = onboardingCase.StudentEnrollmentId,
			StartedAt = onboardingCase.StartedAt,
			ReadyAt = onboardingCase.ReadyAt,
			CompletedAt = onboardingCase.CompletedAt,
			CancelledAt = onboardingCase.CancelledAt,
			CancellationReason = onboardingCase.CancellationReason,
			LastError = onboardingCase.LastError,
			PrimaryBlocker = primaryBlocker,
			ProgressPercent = progressTotal == 0 ? 0 : (int)Math.Round(satisfied * 100d / progressTotal),
			IsOverdue = onboardingCase.Status is not StudentOnboardingStatus.Completed and not StudentOnboardingStatus.Cancelled &&
				DateTime.UtcNow > onboardingCase.DueAt,
			CreationTime = onboardingCase.CreationTime,
			CreatorId = onboardingCase.CreatorId,
			LastModificationTime = onboardingCase.LastModificationTime,
			LastModifierId = onboardingCase.LastModifierId,
			Steps = steps.OrderBy(step => step.DisplayOrder).Select(step => { var dto = MapStep(step); dto.CompletedByName = ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyActorReader.Name(actorNames, step.CompletedByUserId); return dto; }).ToList(),
			Events = events.OrderByDescending(item => item.CreationTime).Select(item => { var dto = MapEvent(item); dto.ActorName = ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyActorMetadata.Name(item.MetadataJson) ?? ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyActorReader.Name(actorNames, item.CreatorId); return dto; }).ToList()
		};
	}

	private static IReadOnlyCollection<StudentOnboardingStep> ProjectFeeReadiness(IReadOnlyCollection<StudentOnboardingStep> steps, bool confirmed) =>
        steps.Select(step => step.Key == StudentOnboardingStepKeys.FinanceAccountsServices &&
            (step.Status == StudentOnboardingStepStatus.Completed) != confirmed
            ? new StudentOnboardingStep(step.Id, step.TenantId, step.OnboardingCaseId, step.Key, step.Label, step.DisplayOrder,
                step.Required, confirmed ? StudentOnboardingStepStatus.Completed : StudentOnboardingStepStatus.Blocked)
            : step).ToList();
	private static StudentOnboardingStepDto MapStep(StudentOnboardingStep step) => new()
	{
		Id = step.Id, Key = step.Key, Label = step.Label, DisplayOrder = step.DisplayOrder,
		Required = step.Required, Status = step.Status, Detail = step.Detail,
		CompletedByUserId = step.CompletedByUserId, CompletedAt = step.CompletedAt, WaiverReason = step.WaiverReason
	};

	private static StudentOnboardingEventDto MapEvent(StudentOnboardingEvent item) => new()
	{
		Id = item.Id, EventType = item.EventType, Description = item.Description,
		MetadataJson = item.MetadataJson, ActorUserId = item.CreatorId, OccurredAt = item.CreationTime
	};

	private static StudentOnboardingPolicyDto MapPolicy(StudentOnboardingPolicy policy) => new()
	{
		Id = policy.Id, ConcurrencyStamp = policy.ConcurrencyStamp, AcademicYearId = policy.AcademicYearId,
		OfferingId = policy.OfferingId, StandardId = policy.StandardId, Name = policy.Name,
		RequireDocuments = policy.RequireDocuments, RequireFamilyDetails = policy.RequireFamilyDetails,
		RequireCommunicationDetails = policy.RequireCommunicationDetails, RequireMedicalDetails = policy.RequireMedicalDetails,
		RequireFeeClearance = policy.RequireFeeClearance, RequireStudentAccount = policy.RequireStudentAccount,
		RequireGuardianAccount = policy.RequireGuardianAccount, RequireServiceConsent = policy.RequireServiceConsent,
		SeatReservationMinutes = policy.SeatReservationMinutes, SlaHours = policy.SlaHours, IsActive = policy.IsActive
	};

	private static StudentOnboardingSettingsDto MapSettings(StudentOnboardingSettings settings) => new()
	{
		Id = settings.Id,
		ConcurrencyStamp = settings.ConcurrencyStamp,
		AcademicYearId = settings.AcademicYearId,
		SchemaVersion = settings.SchemaVersion,
		ConfigurationJson = settings.ConfigurationJson
	};

	private static StudentIdentifierSeriesDto MapSeries(StudentIdentifierSeries series) => new()
	{
		Id = series.Id, ConcurrencyStamp = series.ConcurrencyStamp, AcademicYearId = series.AcademicYearId,
		StandardId = series.StandardId, IdentifierType = series.IdentifierType, Code = series.Code, Name = series.Name,
		Prefix = series.Prefix, Suffix = series.Suffix, Padding = series.Padding, NextSequence = series.NextSequence,
		MaximumSequence = series.MaximumSequence, EffectiveFrom = series.EffectiveFrom, EffectiveUntil = series.EffectiveUntil,
		IsActive = series.IsActive, NextPreview = $"{series.Prefix}{series.NextSequence.ToString().PadLeft(series.Padding, '0')}{series.Suffix}"
	};

	private static StudentOnboardingServiceHandoffDto MapHandoff(StudentOnboardingServiceHandoff handoff) => new()
	{
		Id = handoff.Id, OnboardingCaseId = handoff.OnboardingCaseId, ServiceKey = handoff.ServiceKey,
		Required = handoff.Required, Status = handoff.Status, AttemptCount = handoff.AttemptCount,
		LastAttemptAt = handoff.LastAttemptAt, AcknowledgedAt = handoff.AcknowledgedAt, LastError = handoff.LastError
	};

	private async Task AddEventAsync(Guid caseId, string eventType, string description) =>
		await eventRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyAuditWriter>().CreateOnboardingEvent(caseId, eventType, description), false);

	private async Task<StudentOnboardingCase> GetCaseAsync(Guid id)
	{
		var item = await AsyncExecuter.FirstOrDefaultAsync((await GetScopedCasesAsync()).Where(c => c.Id == id)) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingCase), id);
		if (!item.HasCampusSnapshot)
		{
			var offering = await offeringRepository.FindAsync(item.OfferingId);
			item.UseLegacyCampusId(offering?.CampusId);
		}
		return item;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentOnboardingCampusDto>> GetAvailableCampusesAsync() =>
		(await campusRepository.GetListAsync(c => c.IsActive))
			.OrderBy(c => c.Name).ThenBy(c => c.Code)
			.Select(c => new StudentOnboardingCampusDto { Id = c.Id, Code = c.Code, Name = c.Name })
			.ToList();

	private static bool IsSatisfied(StudentOnboardingStepStatus status) =>
		status is StudentOnboardingStepStatus.Completed or StudentOnboardingStepStatus.Waived or StudentOnboardingStepStatus.NotRequired;

	private static void EnsureConcurrency(StudentOnboardingCase onboardingCase, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || !string.Equals(stamp, onboardingCase.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}

	private static void EnsureConcurrency(StudentOnboardingPolicy policy, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || !string.Equals(stamp, policy.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}

	private static void EnsureConcurrency(StudentOnboardingSettings settings, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) ||
		    !string.Equals(stamp, settings.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
	}
}
