using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService_Tests
{
	[Fact]
	public async Task Reporting_visibility_keeps_descendants_and_excludes_peers_without_employee_graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var childUser = Guid.NewGuid();
			var grandchildUser = Guid.NewGuid();
			var manager = await CreateEmployeeAsync(actor, "Manager");
			var child = await CreateEmployeeAsync(childUser, "Child");
			var grandchild = await CreateEmployeeAsync(grandchildUser, "Grandchild");
			var bridge = await CreateEmployeeAsync(Guid.NewGuid(), "No login bridge");
			var deepUser = Guid.NewGuid();
			var deep = await CreateEmployeeAsync(deepUser, "Deep descendant");
			await CreateEmployeeAsync(Guid.NewGuid(), "Unrelated");
			child.AssignReportingManager(manager.Id);
			grandchild.AssignReportingManager(child.Id);
			bridge.AssignReportingManager(manager.Id);
			typeof(Employee).GetProperty(nameof(Employee.UserId))!.SetValue(bridge, null);
			deep.AssignReportingManager(bridge.Id);
			await _employeeRepository.UpdateManyAsync(new[] { child, grandchild, bridge, deep }, autoSave: true);
			var (_, own) = await SeedRoomAndReservationAsync(actor);
			var (_, childReservation) = await SeedRoomAndReservationAsync(childUser);
			var (_, grandchildReservation) = await SeedRoomAndReservationAsync(grandchildUser);
			var (_, deepReservation) = await SeedRoomAndReservationAsync(deepUser);
			await SeedRoomAndReservationAsync(Guid.NewGuid());
			await _userScopeAssignmentRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), null, actor), autoSave: true);
			await _userScopeService.InvalidateUserScopeCacheAsync(actor);
			await GetRequiredService<IClassTeacherAccessService>().HasAssignmentAsync(actor);
			var db = await RoomDatabaseAsync();
			db.ChangeTracker.Clear();
			using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "teacher")))
			{
				var page = await _roomAvailabilityAppService.GetReservationsPageAsync(new GetRoomReservationsPageInputDto { MaxResultCount = 100, IsApprovalView = true });
				page.Items.Select(row => row.Id).OrderBy(id => id).ShouldBe(
					new[] { own.Id, childReservation.Id, grandchildReservation.Id, deepReservation.Id }.OrderBy(id => id));
				page.Items.Single(row => row.Id == own.Id).CanReview.ShouldBeFalse();
				page.Items.Where(row => row.Id != own.Id).ShouldAllBe(row => row.CanReview);
				await _roomAvailabilityAppService.ApproveReservationAsync(deepReservation.Id);
			}
			db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task Suggestions_enrich_only_returned_building_floor_and_room_type_references()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var input = new SuggestedRoomRequestDto { Date = new DateTime(2030, 2, 4), StartTime = new TimeSpan(9, 0, 0),
				EndTime = new TimeSpan(10, 0, 0), RequiredCapacity = room.Capacity, Limit = 1 };
			var db = await RoomDatabaseAsync();
			db.ChangeTracker.Clear();
			string expected;
			int buildings, floors, types;
			using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
			{
				expected = JsonSerializer.Serialize(await _roomAvailabilityAppService.SuggestRoomsAsync(input));
				buildings = small.ReadsFor("academic_Buildings");
				floors = small.ReadsFor("academic_Floors");
				types = small.ReadsFor("academic_RoomTypes");
			}
			for (var index = 0; index < 12; index++)
			{
				var building = new Building { BuildingName = $"Unrelated {index}" };
				SetEntityId(building, Guid.NewGuid());
				await _buildingRepository.InsertAsync(building, autoSave: true);
				var floor = new Floor { BuildingId = building.Id, FloorName = $"Unrelated {index}", FloorNo = 0 };
				SetEntityId(floor, Guid.NewGuid());
				await _floorRepository.InsertAsync(floor, autoSave: true);
				var type = new RoomType { RoomTypeName = $"Unrelated {index}", ShortName = $"U{index}" };
				SetEntityId(type, Guid.NewGuid());
				await _roomTypeRepository.InsertAsync(type, autoSave: true);
			}
			db.ChangeTracker.Clear();
			using var large = new TimetableReadCapture(db.Database.GetDbConnection());
			JsonSerializer.Serialize(await _roomAvailabilityAppService.SuggestRoomsAsync(input)).ShouldBe(expected);
			large.ReadsFor("academic_Buildings").ShouldBeLessThanOrEqualTo(buildings);
			large.ReadsFor("academic_Floors").ShouldBeLessThanOrEqualTo(floors);
			large.ReadsFor("academic_RoomTypes").ShouldBeLessThanOrEqualTo(types);
			db.ChangeTracker.Entries<Building>().ShouldBeEmpty();
			db.ChangeTracker.Entries<Floor>().ShouldBeEmpty();
			db.ChangeTracker.Entries<RoomType>().ShouldBeEmpty();
		});
	}
}
