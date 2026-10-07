using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionClassCapacityReadService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionClassRoomAllocation, Guid> allocationRepository,
	IRepository<Room, Guid> roomRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionClassCapacityDto> ReadAsync(AdmissionOffering offering, AdmissionCycle cycle)
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
