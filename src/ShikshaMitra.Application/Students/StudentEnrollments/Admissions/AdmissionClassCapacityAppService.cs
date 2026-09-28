using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionClassCapacityAppService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionClassRoomAllocation, Guid> allocationRepository,
	IRepository<Room, Guid> roomRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	AdmissionApplicationScopeAccessService applicationScope)
	: ApplicationService, IAdmissionClassCapacityAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public Task<AdmissionClassCapacityDto> GetForOfferingAsync(Guid offeringId) => ReadForOfferingAsync(offeringId);

	[RemoteService(false)]
	public async Task<AdmissionClassCapacityDto> ReadForOfferingAsync(Guid offeringId)
	{
		var (offering, cycle) = await GetScopedOfferingAsync(offeringId);
		return await BuildSummaryAsync(offering, cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<List<AdmissionRoomChoiceDto>> GetRoomChoicesAsync(Guid offeringId, string? search)
	{
		var (offering, cycle) = await GetScopedOfferingAsync(offeringId);
		var roomQuery = (await roomRepository.GetQueryableAsync()).Where(room => room.Capacity > 0);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim();
			roomQuery = roomQuery.Where(room => room.RoomName.Contains(term));
		}
		var rooms = await AsyncExecuter.ToListAsync(roomQuery.OrderBy(room => room.RoomName).Take(100));
		var roomIds = rooms.Select(room => room.Id).ToList();
		var assigned = roomIds.Count == 0 ? [] : await allocationRepository.GetListAsync(item =>
			item.AcademicYearId == cycle.AcademicYearId && roomIds.Contains(item.RoomId));
		var allocatedElsewhere = assigned.Where(item => item.StandardId != offering.StandardId)
			.Select(item => item.RoomId).ToHashSet();
		return rooms.Select(room => new AdmissionRoomChoiceDto
		{
			RoomId = room.Id, RoomName = room.RoomName, Capacity = room.Capacity,
			IsAllocatedToAnotherClass = allocatedElsewhere.Contains(room.Id)
		}).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<AdmissionClassCapacityDto> UpdateForOfferingAsync(Guid offeringId, UpdateAdmissionClassRoomsDto input)
	{
		var (offering, cycle) = await GetScopedOfferingAsync(offeringId);
		if (input?.RoomIds == null || input.RoomIds.Any(id => id == Guid.Empty) ||
			input.RoomIds.Count != input.RoomIds.Distinct().Count())
			throw new BusinessException(AdmissionErrorCodes.ClassRoomAllocationInvalid,
				"Choose each valid room only once.");

		var selected = input.RoomIds;
		var rooms = selected.Count == 0 ? [] : await roomRepository.GetListAsync(room => selected.Contains(room.Id));
		if (rooms.Count != selected.Count || rooms.Any(room => room.Capacity <= 0))
			throw new BusinessException(AdmissionErrorCodes.ClassRoomAllocationInvalid,
				"Every selected room must exist and have a positive capacity.");

		var existing = await allocationRepository.GetListAsync(item =>
			item.AcademicYearId == cycle.AcademicYearId && item.StandardId == offering.StandardId);
		var otherClassRooms = await allocationRepository.GetListAsync(item =>
			item.AcademicYearId == cycle.AcademicYearId && item.StandardId != offering.StandardId && selected.Contains(item.RoomId));
		if (otherClassRooms.Count > 0)
			throw new BusinessException(AdmissionErrorCodes.ClassRoomAlreadyAllocated,
				"A selected room is already assigned to another class in this academic year.");

		foreach (var item in existing.Where(item => !selected.Contains(item.RoomId)))
			await allocationRepository.DeleteAsync(item, autoSave: false);
		// Free the filtered unique index before adding replacement allocations.
		await UnitOfWorkManager.Current!.SaveChangesAsync();
		foreach (var roomId in selected.Where(id => existing.All(item => item.RoomId != id)))
			await allocationRepository.InsertAsync(new AdmissionClassRoomAllocation(
				GuidGenerator.Create(), CurrentTenant.Id, cycle.AcademicYearId, offering.StandardId, roomId), autoSave: false);
		try
		{
			await UnitOfWorkManager.Current!.SaveChangesAsync();
		}
		catch (Exception exception) when (IsUniqueConstraintViolation(exception))
		{
			throw new BusinessException(AdmissionErrorCodes.ClassRoomAlreadyAllocated,
				"A selected room was assigned to another class. Refresh the room list and try again.");
		}
		return await BuildSummaryAsync(offering, cycle);
	}

	private static bool IsUniqueConstraintViolation(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
			if (current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString() == "23505" ||
				current.Message.Contains("23505", StringComparison.Ordinal))
				return true;
		return false;
	}

	private async Task<(AdmissionOffering Offering, AdmissionCycle Cycle)> GetScopedOfferingAsync(Guid offeringId)
	{
		var offering = await AsyncExecuter.FirstOrDefaultAsync(
			(await applicationScope.GetScopedOfferingsAsync()).Where(item => item.Id == offeringId)) ??
			throw new EntityNotFoundException(typeof(AdmissionOffering), offeringId);
		var cycle = await cycleRepository.FindAsync(offering.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), offering.AdmissionCycleId);
		return (offering, cycle);
	}

	private async Task<AdmissionClassCapacityDto> BuildSummaryAsync(AdmissionOffering offering, AdmissionCycle cycle)
	{
		var allocations = await allocationRepository.GetListAsync(item =>
			item.AcademicYearId == cycle.AcademicYearId && item.StandardId == offering.StandardId);
		var roomIds = allocations.Select(item => item.RoomId).ToList();
		var rooms = roomIds.Count == 0 ? [] : await roomRepository.GetListAsync(room => roomIds.Contains(room.Id));
		var activeStudentIds = await AsyncExecuter.ToListAsync((await enrollmentRepository.GetQueryableAsync())
			.Where(item => item.AcademicYearId == cycle.AcademicYearId && item.StandardId == offering.StandardId && item.IsActive)
			.Select(item => item.StudentId).Distinct());
		var activeIds = activeStudentIds.ToHashSet();

		var accepted = await AsyncExecuter.ToListAsync(
			from application in await applicationRepository.GetQueryableAsync()
			join admissionOffering in await offeringRepository.GetQueryableAsync()
				on application.AdmissionOfferingId equals admissionOffering.Id
			join admissionCycle in await cycleRepository.GetQueryableAsync()
				on admissionOffering.AdmissionCycleId equals admissionCycle.Id
			join applicant in await applicantRepository.GetQueryableAsync()
				on application.ApplicantId equals applicant.Id
			where admissionCycle.AcademicYearId == cycle.AcademicYearId && admissionOffering.StandardId == offering.StandardId &&
				(application.Status == AdmissionApplicationStatus.OfferAccepted ||
				 application.Status == AdmissionApplicationStatus.AdmissionFinalized)
			select new { application.ApplicantId, application.StudentId, application.EnrolledStudentId, applicant.ExistingStudentId });
		var pendingCount = accepted.Where(item =>
			(!item.StudentId.HasValue || !activeIds.Contains(item.StudentId.Value)) &&
			(!item.EnrolledStudentId.HasValue || !activeIds.Contains(item.EnrolledStudentId.Value)) &&
			(!item.ExistingStudentId.HasValue || !activeIds.Contains(item.ExistingStudentId.Value)))
			.Select(item => item.ApplicantId).Distinct().Count();
		var occupied = activeIds.Count + pendingCount;
		var capacity = roomIds.Count == 0 || rooms.Count != roomIds.Count ? (int?)null : rooms.Sum(room => room.Capacity);
		var state = AdmissionClassCapacityCalculator.Calculate(capacity, occupied);
		return new AdmissionClassCapacityDto
		{
			AdmissionCycleId = cycle.Id,
			AcademicYearId = cycle.AcademicYearId,
			StandardId = offering.StandardId,
			Rooms = rooms.OrderBy(room => room.RoomName).Select(room => new AdmissionClassRoomDto
			{
				RoomId = room.Id, RoomName = room.RoomName, Capacity = room.Capacity
			}).ToList(),
			Capacity = capacity,
			OccupiedCount = occupied,
			RemainingSeats = state.RemainingSeats,
			IsFull = state.IsFull,
			IsOverCapacity = state.IsOverCapacity,
			Warning = state.Warning
		};
	}
}
