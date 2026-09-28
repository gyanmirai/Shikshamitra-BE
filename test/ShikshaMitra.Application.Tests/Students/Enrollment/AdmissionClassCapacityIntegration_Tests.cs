using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Capacity_releases_enrolled_seat_after_student_enrollment_is_deactivated()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId, description: "Inactive enrollment capacity test"),
				autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var room = await CreateCapacityRoomAsync(1);
			var capacityService = GetRequiredService<IAdmissionClassCapacityAppService>();
			await capacityService.UpdateForOfferingAsync(offering.Id,
				new UpdateAdmissionClassRoomsDto { RoomIds = [room.Id] });
			var student = await CreateExistingStudentAsync(offering, "Transfer", "Patel");
			var applicant = await CreateApplicantAsync("Transfer");
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
			var enrollmentId = Guid.NewGuid();
			await WithUnitOfWorkAsync(async () =>
			{
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				var enrollment = StudentEnrollment.Create(enrollmentId, TestTenantId, student.Id,
					cycle.AcademicYearId, offering.StandardId, null);
				await _studentEnrollmentRepository.InsertAsync(enrollment, autoSave: true);
				var stored = await _applicationRepository.GetAsync(application.Id);
				stored.Submit();
				stored.MarkInReview(TestUserId, null);
				stored.Approve(TestUserId, null);
				stored.IssueOffer(DateTime.UtcNow.AddDays(7), TestUserId, null);
				stored.AcceptOffer(TestUserId, null);
				stored.MarkEnrolled(student.Id, enrollmentId);
				await _applicationRepository.UpdateAsync(stored, autoSave: true);
			});
			(await capacityService.GetForOfferingAsync(offering.Id)).OccupiedCount.ShouldBe(1);
			await WithUnitOfWorkAsync(async () =>
			{
				var enrollment = await _studentEnrollmentRepository.GetAsync(enrollmentId);
				enrollment.Deactivate();
				await _studentEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
			});
			var released = await capacityService.GetForOfferingAsync(offering.Id);
			released.OccupiedCount.ShouldBe(0);
			released.RemainingSeats.ShouldBe(1);
		}
	}

	private Task<Room> CreateCapacityRoomAsync(int capacity) => WithUnitOfWorkAsync(async () =>
	{
		var building = new Building { TenantId = TestTenantId, BuildingName = "Admission capacity block", TotalFloors = 1 };
		AssignCapacityEntityId(building);
		await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, autoSave: true);
		var floor = new Floor { BuildingId = building.Id, FloorName = "Ground", FloorNo = 0, TotalRoom = 1 };
		AssignCapacityEntityId(floor);
		await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, autoSave: true);
		var type = new RoomType { TenantId = TestTenantId, RoomTypeName = "Classroom", ShortName = "CLS" };
		AssignCapacityEntityId(type);
		await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(type, autoSave: true);
		var room = new Room
		{
			TenantId = TestTenantId, RoomName = "Capacity room", BuildingId = building.Id,
			FloorId = floor.Id, RoomTypeId = type.Id, Capacity = capacity
		};
		AssignCapacityEntityId(room);
		return await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, autoSave: true);
	});

	private static void AssignCapacityEntityId<TEntity>(TEntity entity) where TEntity : Entity<Guid> =>
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());
}
