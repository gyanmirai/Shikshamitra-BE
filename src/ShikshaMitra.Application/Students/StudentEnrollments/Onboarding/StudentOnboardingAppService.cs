using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
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
public class StudentOnboardingAppService(
	IRepository<StudentOnboardingCase, Guid> caseRepository,
	IRepository<StudentOnboardingStep, Guid> stepRepository,
	IRepository<StudentOnboardingEvent, Guid> eventRepository,
	IRepository<StudentOnboardingPolicy, Guid> policyRepository,
	IRepository<StudentSeatReservation, Guid> reservationRepository,
	IRepository<StudentIdentifierSeries, Guid> seriesRepository,
	IRepository<StudentIdentifierAllocation, Guid> allocationRepository,
	IRepository<StudentOnboardingServiceHandoff, Guid> handoffRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	IStudentRepository studentRepository,
	StudentDocumentManager studentDocumentManager,
	AdmissionStudentEvidenceReader admissionEvidenceReader,
	IIdentityUserRepository identityUserRepository,
	IDistributedEventBus distributedEventBus,
	IUnitOfWorkManager unitOfWorkManager)
	: ApplicationService, IStudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<PagedResultDto<StudentOnboardingCaseDto>> GetListAsync(StudentOnboardingListInput input)
	{
		var query = await caseRepository.GetQueryableAsync();
		if (input.Status.HasValue) query = query.Where(item => item.Status == input.Status.Value);
		if (input.AcademicYearId.HasValue) query = query.Where(item => item.AcademicYearId == input.AcademicYearId.Value);
		if (input.StandardId.HasValue) query = query.Where(item => item.IntendedStandardId == input.StandardId.Value);
		if (input.AssignedToUserId.HasValue) query = query.Where(item => item.AssignedToUserId == input.AssignedToUserId.Value);
		if (input.UnassignedOnly == true) query = query.Where(item => !item.AssignedToUserId.HasValue);
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

		var totalCount = await AsyncExecuter.CountAsync(query);
		var cases = await AsyncExecuter.ToListAsync(query.OrderByDescending(item => item.CreationTime)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var rows = new List<StudentOnboardingCaseDto>(cases.Count);
		foreach (var onboardingCase in cases)
			rows.Add(await MapCaseAsync(onboardingCase, includeEvents: false));
		return new PagedResultDto<StudentOnboardingCaseDto>(totalCount, rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingFilterOptionsDto> GetFilterOptionsAsync()
	{
		var query = await caseRepository.GetQueryableAsync();
		var academicYearIds = await AsyncExecuter.ToListAsync(query
			.Select(item => item.AcademicYearId).Distinct());
		var standardIds = await AsyncExecuter.ToListAsync(query
			.Select(item => item.IntendedStandardId).Distinct());
		var academicYears = academicYearIds.Count == 0
			? []
			: await academicYearRepository.GetListAsync(item => academicYearIds.Contains(item.Id));
		var standards = standardIds.Count == 0
			? []
			: await standardRepository.GetListAsync(item => standardIds.Contains(item.Id));
		return new StudentOnboardingFilterOptionsDto
		{
			AcademicYears = academicYears.OrderByDescending(item => item.Name)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name }).ToList(),
			Standards = standards.OrderBy(item => item.Name)
				.Select(item => new StudentOnboardingFilterOptionDto { Id = item.Id, Name = item.Name }).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingAnalyticsDto> GetAnalyticsAsync()
	{
		var query = await caseRepository.GetQueryableAsync();
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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	public async Task<IReadOnlyList<StudentOnboardingPlacementOptionDto>> GetPlacementOptionsAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var divisions = await divisionRepository.GetListAsync(item =>
			item.StandardId == onboardingCase.IntendedStandardId);
		var now = DateTime.UtcNow;
		var options = new List<StudentOnboardingPlacementOptionDto>(divisions.Count);
		foreach (var division in divisions.OrderBy(item => item.Name))
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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetAvailableIdentifierSeriesAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var now = DateTime.UtcNow;
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
	public async Task<StudentOnboardingCaseDto> AssignOwnerAsync(Guid id, AssignStudentOnboardingOwnerDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		if (input.UserId.HasValue && await identityUserRepository.FindAsync(input.UserId.Value) == null)
			throw new BusinessException(StudentOnboardingErrorCodes.OwnerInvalid);
		onboardingCase.AssignOwner(input.UserId);
		await AddEventAsync(id, "OwnerChanged", input.UserId.HasValue ? "Onboarding owner assigned." : "Onboarding owner cleared.");
		await caseRepository.UpdateAsync(onboardingCase, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
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
				if (step.Key == StudentOnboardingStepKeys.FinalReadiness)
					throw new BusinessException(StudentOnboardingErrorCodes.InvalidStep);
				step.Complete(CurrentUser.Id, now, input.Detail);
				break;
			case StudentOnboardingStepStatus.Blocked:
				step.Block(input.Detail ?? "Action is required before this step can continue.");
				break;
			case StudentOnboardingStepStatus.Waived:
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
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		var division = await divisionRepository.FindAsync(input.DivisionId);
		if (division == null || division.StandardId != onboardingCase.IntendedStandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);

		var now = DateTime.UtcNow;
		var placementChanged = onboardingCase.DivisionId.HasValue && onboardingCase.DivisionId != division.Id;
		if (onboardingCase.SeatReservationId.HasValue)
		{
			var current = await reservationRepository.FindAsync(onboardingCase.SeatReservationId.Value);
			if (current?.DivisionId == division.Id && current.IsLive(now))
				return await MapCaseAsync(onboardingCase, true);
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
		onboardingCase.RecordPlacement(division.Id, reservation.Id, now);
		if (placementChanged && onboardingCase.RollNumber.HasValue)
		{
			onboardingCase.ClearRollNumberForPlacementChange(now);
			await ResetSystemStepAsync(id, StudentOnboardingStepKeys.IdentifiersAndRoll,
				"Division changed; select a roll number that is available in the new division.");
		}
		await CompleteSystemStepAsync(id, StudentOnboardingStepKeys.PlacementAndSeat,
			$"Division {division.Name} reserved (capacity slot {seatNumber}).", now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "SeatReserved", $"Division {division.Name} reserved until {reservation.ExpiresAt:u}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task<StudentOnboardingCaseDto> AllocateIdentifierAsync(
		Guid id, AllocateStudentOnboardingIdentifierDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
		if (!onboardingCase.DivisionId.HasValue)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
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
		Guid? allocationId = onboardingCase.IdentifierAllocationId;
		if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
		{
			var existing = await allocationRepository.FindAsync(item => item.OnboardingCaseId == id &&
				item.Status == StudentIdentifierAllocationStatus.Reserved);
			if (existing != null)
			{
				allocationId = existing.Id;
			}
			else
			{
				var series = await seriesRepository.FindAsync(input.NumberSeriesId) ??
					throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
				if (!series.IsActive || series.AcademicYearId != onboardingCase.AcademicYearId ||
				    series.IdentifierType is not (StudentIdentifierType.GrNumber or StudentIdentifierType.AdmissionNumber) ||
				    series.StandardId.HasValue && series.StandardId != onboardingCase.IntendedStandardId)
					throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
				series.EnsureAvailable(now);
				long sequence;
				string value;
				var isManual = !string.IsNullOrWhiteSpace(input.ManualValue);
				if (isManual)
				{
					if (string.IsNullOrWhiteSpace(input.OverrideReason))
						throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
					sequence = input.ManualSequence ?? 0;
					value = input.ManualValue!.Trim();
				}
				else
				{
					(sequence, value) = series.Allocate(now);
					await seriesRepository.UpdateAsync(series, false);
				}
				var allocation = new StudentIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id, id,
					student.Id, series.Id, series.IdentifierType, sequence, value, now, isManual, input.OverrideReason);
				await allocationRepository.InsertAsync(allocation, false);
				allocationId = allocation.Id;
			}
		}

		onboardingCase.RecordIdentifier(allocationId, input.RollNumber, now);
		await CompleteSystemStepAsync(id, StudentOnboardingStepKeys.IdentifiersAndRoll,
			$"Identifiers prepared; roll number {input.RollNumber} reserved for activation.", now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await AddEventAsync(id, "IdentifiersPrepared", $"Identifier and roll number {input.RollNumber} prepared.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCaseAsync(onboardingCase, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<StudentOnboardingCaseDto> PreviewActivationAsync(Guid id)
	{
		var onboardingCase = await GetCaseAsync(id);
		var steps = await stepRepository.GetListAsync(item => item.OnboardingCaseId == id);
		var blockers = await GetActivationBlockersAsync(onboardingCase, steps, DateTime.UtcNow);
		var dto = await MapCaseAsync(onboardingCase, true, steps);
		if (blockers.Count == 0 && onboardingCase.Status is not StudentOnboardingStatus.Completed and not StudentOnboardingStatus.Cancelled)
			dto.Status = StudentOnboardingStatus.ReadyForActivation;
		dto.LastError = blockers.Count == 0 ? null : string.Join("; ", blockers);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate)]
	public async Task<StudentOnboardingCaseDto> ActivateAsync(Guid id, ActivateStudentOnboardingDto input)
	{
		try
		{
			return await ActivateCoreAsync(id, input);
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
	}

	private async Task<StudentOnboardingCaseDto> ActivateCoreAsync(Guid id, ActivateStudentOnboardingDto input)
	{
		var onboardingCase = await GetCaseAsync(id);
		if (onboardingCase.Status == StudentOnboardingStatus.Completed)
			return await MapCaseAsync(onboardingCase, true);
		EnsureConcurrency(onboardingCase, input.ConcurrencyStamp);
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
		if (await enrollmentRepository.AnyAsync(item => item.StudentId == student.Id &&
			item.AcademicYearId == onboardingCase.AcademicYearId && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.EnrollmentAlreadyExists);
		if (await enrollmentRepository.AnyAsync(item => item.AcademicYearId == onboardingCase.AcademicYearId &&
			item.DivisionId == onboardingCase.DivisionId && item.RollNumber == onboardingCase.RollNumber && item.IsActive))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);

		var enrollment = StudentEnrollment.Create(GuidGenerator.Create(), CurrentTenant.Id, student.Id,
			onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId, onboardingCase.DivisionId,
			onboardingCase.RollNumber, onboardingCase.Id);
		await enrollmentRepository.InsertAsync(enrollment, false);
		if (allocation != null)
		{
			var legacyGrNo = int.TryParse(allocation.Value, out var parsed) ? parsed : (int?)null;
			student.AssignGrNumber(allocation.Value, legacyGrNo);
			await allocationRepository.UpdateAsync(allocation, false);
		}
		student.UpdateAcademicPlacement(onboardingCase.AcademicYearId, onboardingCase.IntendedBoardId,
			onboardingCase.IntendedMediumId, onboardingCase.IntendedStageId, onboardingCase.IntendedStandardId,
			onboardingCase.DivisionId);
		student.AssignRollNo(onboardingCase.RollNumber);
		student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		await studentRepository.UpdateAsync(student, false);
		await reservationRepository.UpdateAsync(reservation, false);
		onboardingCase.Complete(enrollment.Id, now);
		await caseRepository.UpdateAsync(onboardingCase, false);
		await EnsureServiceHandoffsAsync(onboardingCase);
		await AddEventAsync(id, "Activated", "Onboarding completed and the Student became operational.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		await distributedEventBus.PublishAsync(new StudentOnboardingCompletedEto(onboardingCase.Id, student.Id,
			enrollment.Id, onboardingCase.AcademicYearId, onboardingCase.IntendedStandardId,
			onboardingCase.DivisionId!.Value, CurrentTenant.Id, now));
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
			await eventRepository.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id,
				id, "ActivationFailed", reason), false);
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
		await GetCaseAsync(id);
		var handoff = await handoffRepository.FindAsync(item => item.Id == handoffId && item.OnboardingCaseId == id) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingServiceHandoff), handoffId);
		handoff.QueueRetry();
		await handoffRepository.UpdateAsync(handoff, false);
		await AddEventAsync(id, "HandoffRetryQueued", $"Retry queued for {handoff.ServiceKey}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return MapHandoff(handoff);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<IReadOnlyList<StudentOnboardingPolicyDto>> GetPoliciesAsync(Guid academicYearId) =>
		(await policyRepository.GetListAsync(item => item.AcademicYearId == academicYearId))
		.OrderByDescending(item => item.OfferingId.HasValue).ThenByDescending(item => item.StandardId.HasValue)
		.Select(MapPolicy).ToList();

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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<IReadOnlyList<StudentIdentifierSeriesDto>> GetIdentifierSeriesAsync(Guid academicYearId) =>
		(await seriesRepository.GetListAsync(item => item.AcademicYearId == academicYearId))
		.OrderBy(item => item.Code).Select(MapSeries).ToList();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	public async Task<StudentIdentifierSeriesDto> CreateIdentifierSeriesAsync(CreateStudentIdentifierSeriesDto input)
	{
		if (!await academicYearRepository.AnyAsync(item => item.Id == input.AcademicYearId) ||
		    input.StandardId.HasValue && !await standardRepository.AnyAsync(item => item.Id == input.StandardId.Value))
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
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
		if (string.IsNullOrWhiteSpace(concurrencyStamp) ||
		    !string.Equals(concurrencyStamp, series.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		series.Deactivate();
		await seriesRepository.UpdateAsync(series, true);
		return MapSeries(series);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public Task<HistoricalAdmissionReconciliationDto> GetHistoricalReconciliationAsync() =>
		ReconcileHistoricalAdmissionsAsync(apply: false);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public Task<HistoricalAdmissionReconciliationDto> BackfillHistoricalAdmissionsAsync() =>
		ReconcileHistoricalAdmissionsAsync(apply: true);

	private async Task<HistoricalAdmissionReconciliationDto> ReconcileHistoricalAdmissionsAsync(bool apply)
	{
		var applications = await applicationRepository.GetListAsync(item =>
			item.Status == AdmissionApplicationStatus.Enrolled);
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
			await eventRepository.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id,
				onboardingCase.Id, "HistoricalBackfill",
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
		if (!input.OfferingId.HasValue) return;
		var offering = await offeringRepository.FindAsync(input.OfferingId.Value);
		if (offering == null || offering.StandardId != input.StandardId)
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		var cycle = await cycleRepository.FindAsync(offering.AdmissionCycleId);
		if (cycle == null || cycle.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	private async Task<List<string>> GetActivationBlockersAsync(
		StudentOnboardingCase onboardingCase, IReadOnlyCollection<StudentOnboardingStep> steps, DateTime now)
	{
		var blockers = steps.Where(step => step.Key != StudentOnboardingStepKeys.FinalReadiness &&
			step.Required && !IsSatisfied(step.Status)).Select(step => step.Label).ToList();
		if (!onboardingCase.DivisionId.HasValue || !onboardingCase.SeatReservationId.HasValue)
			blockers.Add("Academic placement and a live seat reservation are required.");
		else
		{
			var reservation = await reservationRepository.FindAsync(onboardingCase.SeatReservationId.Value);
			if (reservation == null || !reservation.IsLive(now)) blockers.Add("The seat reservation has expired.");
		}
		if (!onboardingCase.RollNumber.HasValue) blockers.Add("A roll number is required.");
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
		IReadOnlyCollection<StudentOnboardingStep> steps, DateTime at)
	{
		var student = await studentRepository.GetWithDetailsAsync(onboardingCase.StudentId) ??
			throw new EntityNotFoundException(typeof(Student), onboardingCase.StudentId);
		SetComputedStep(steps, StudentOnboardingStepKeys.Identity,
			!string.IsNullOrWhiteSpace(student.FirstName) && !string.IsNullOrWhiteSpace(student.LastName) &&
			student.AdditionalDetail?.DateOfBirth.HasValue == true && student.AdditionalDetail.Gender.HasValue,
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
		var documentStatus = await studentDocumentManager.GetRequiredDocumentsStatusAsync(student.Id);
		var acceptedAdmissionDocumentTypes = await admissionEvidenceReader.GetAcceptedUploadDocumentTypeIdsAsync(student.Id);
		var documentsReady = documentStatus.Where(item => item.documentType.IsRequired)
			.All(item => item.uploadedDocument?.ValidatedAt.HasValue == true ||
				acceptedAdmissionDocumentTypes.Contains(item.documentType.Id));
		SetComputedStep(steps, StudentOnboardingStepKeys.Documents, documentsReady,
			"Required documents have validated Student uploads or accepted admission uploads.",
			"Provide a validated Student upload or an accepted admission upload for every required document type.", at);
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
		if (!step.Required || step.Status == StudentOnboardingStepStatus.Waived) return;
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
		// Optional services need an explicit family/staff selection. Until that selection is stored,
		// do not fabricate transport, hostel, meal, uniform, device, library, or health requests.
		var definitions = new List<(string Key, bool Required)>();
		if (policy?.RequireFeeClearance == true) definitions.Add(("finance", true));
		if (policy?.RequireStudentAccount == true) definitions.Add(("student-account", true));
		if (policy?.RequireGuardianAccount == true) definitions.Add(("guardian-account", true));
		if (policy?.RequireServiceConsent == true) definitions.Add(("service-consent", true));
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
		IReadOnlyCollection<StudentOnboardingStep>? loadedSteps = null)
	{
		var application = await applicationRepository.FindAsync(onboardingCase.ApplicationId);
		var offering = await offeringRepository.FindAsync(onboardingCase.OfferingId);
		var academicYear = await academicYearRepository.FindAsync(onboardingCase.AcademicYearId);
		var standard = await standardRepository.FindAsync(onboardingCase.IntendedStandardId);
		var division = onboardingCase.DivisionId.HasValue
			? await divisionRepository.FindAsync(onboardingCase.DivisionId.Value)
			: null;
		var student = await studentRepository.FindAsync(onboardingCase.StudentId);
		var owner = onboardingCase.AssignedToUserId.HasValue
			? await identityUserRepository.FindAsync(onboardingCase.AssignedToUserId.Value)
			: null;
		var steps = loadedSteps ?? await stepRepository.GetListAsync(item => item.OnboardingCaseId == onboardingCase.Id);
		var events = includeEvents
			? await eventRepository.GetListAsync(item => item.OnboardingCaseId == onboardingCase.Id)
			: [];
		var satisfied = steps.Count(step => IsSatisfied(step.Status));
		var primaryBlocker = steps.OrderBy(step => step.DisplayOrder)
			.FirstOrDefault(step => step.Key != StudentOnboardingStepKeys.FinalReadiness &&
				step.Status == StudentOnboardingStepStatus.Blocked)?.Label ??
			steps.OrderBy(step => step.DisplayOrder)
				.FirstOrDefault(step => step.Key != StudentOnboardingStepKeys.FinalReadiness &&
					step.Required && !IsSatisfied(step.Status))?.Label;
		return new StudentOnboardingCaseDto
		{
			Id = onboardingCase.Id,
			ConcurrencyStamp = onboardingCase.ConcurrencyStamp,
			ApplicationId = onboardingCase.ApplicationId,
			ApplicationNumber = application?.ApplicationNumber ?? string.Empty,
			ApplicantId = onboardingCase.ApplicantId,
			StudentId = onboardingCase.StudentId,
			StudentName = student == null ? string.Empty : string.Join(" ",
				new[] { student.FirstName, student.MiddleName, student.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
			OfferingId = onboardingCase.OfferingId,
			OfferingCode = offering?.Code ?? string.Empty,
			AcademicYearId = onboardingCase.AcademicYearId,
			AcademicYearName = academicYear?.Name ?? string.Empty,
			IntendedBoardId = onboardingCase.IntendedBoardId,
			IntendedMediumId = onboardingCase.IntendedMediumId,
			IntendedStageId = onboardingCase.IntendedStageId,
			IntendedStandardId = onboardingCase.IntendedStandardId,
			IntendedStandardName = standard?.Name ?? string.Empty,
			Status = onboardingCase.Status,
			AssignedToUserId = onboardingCase.AssignedToUserId,
			AssignedToUserName = owner == null ? null :
				(string.IsNullOrWhiteSpace(owner.Name) && string.IsNullOrWhiteSpace(owner.Surname)
					? owner.UserName
					: $"{owner.Name} {owner.Surname}".Trim()),
			DueAt = onboardingCase.DueAt,
			IsHistoricalBackfill = onboardingCase.IsHistoricalBackfill,
			DivisionId = onboardingCase.DivisionId,
			DivisionName = division?.Name,
			SeatReservationId = onboardingCase.SeatReservationId,
			GrNumber = student?.GrNumber ?? student?.GrNo?.ToString(),
			RollNumber = onboardingCase.RollNumber,
			StudentEnrollmentId = onboardingCase.StudentEnrollmentId,
			StartedAt = onboardingCase.StartedAt,
			ReadyAt = onboardingCase.ReadyAt,
			CompletedAt = onboardingCase.CompletedAt,
			CancelledAt = onboardingCase.CancelledAt,
			CancellationReason = onboardingCase.CancellationReason,
			LastError = onboardingCase.LastError,
			PrimaryBlocker = primaryBlocker,
			ProgressPercent = steps.Count == 0 ? 0 : (int)Math.Round(satisfied * 100d / steps.Count),
			IsOverdue = onboardingCase.Status is not StudentOnboardingStatus.Completed and not StudentOnboardingStatus.Cancelled &&
				DateTime.UtcNow > onboardingCase.DueAt,
			CreationTime = onboardingCase.CreationTime,
			CreatorId = onboardingCase.CreatorId,
			LastModificationTime = onboardingCase.LastModificationTime,
			LastModifierId = onboardingCase.LastModifierId,
			Steps = steps.OrderBy(step => step.DisplayOrder).Select(MapStep).ToList(),
			Events = events.OrderByDescending(item => item.CreationTime).Select(MapEvent).ToList()
		};
	}

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
		await eventRepository.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id,
			caseId, eventType, description), false);

	private async Task<StudentOnboardingCase> GetCaseAsync(Guid id) => await caseRepository.FindAsync(id) ??
		throw new EntityNotFoundException(typeof(StudentOnboardingCase), id);

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
}
