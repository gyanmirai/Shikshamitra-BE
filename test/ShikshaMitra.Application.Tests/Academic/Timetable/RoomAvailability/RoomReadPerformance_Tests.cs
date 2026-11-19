using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService_Tests
{
	private static readonly JsonSerializerOptions PageJson = new(JsonSerializerDefaults.Web);

	[Fact]
	public async Task Approval_Page_Should_Preserve_Visible_Date_Created_And_Missing_Conflict_Column_Controls()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (room, _) = await SeedRoomAndReservationAsync(actor);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "admin")))
			{
				var row = new RoomReservation(Guid.NewGuid(), null, room.Id, new DateTime(2030, 1, 7),
					new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), "Approval controls", RoomReservationType.Meeting);
				typeof(RoomReservation).GetProperty(nameof(RoomReservation.CreationTime))!.SetValue(row, new DateTime(2030, 1, 7, 16, 5, 0));
				await _reservationRepository.InsertAsync(row, autoSave: true);
				var created = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ IsApprovalView = true, MaxResultCount = 5, SearchColumns = new[] { "createdAt" }, SearchTerm = "07-01-2030, 04:05 PM" });
				created.GetProperty("totalCount").GetInt64().ShouldBe(1);
				created.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(row.Id);
				var dateAndTime = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ IsApprovalView = true, MaxResultCount = 5, SearchColumns = new[] { "date" }, SearchTerm = "08:00 – 09:00" });
				dateAndTime.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetGuid() == row.Id);
				var conflictText = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ IsApprovalView = true, MaxResultCount = 5, SearchColumns = new[] { "conflictStatus" }, SearchTerm = "none" });
				conflictText.GetProperty("totalCount").GetInt64().ShouldBe(2);
				var missingEnum = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ IsApprovalView = true, MaxResultCount = 5, Filters = "[{\"column\":\"conflictStatus\",\"type\":\"equals\",\"value\":\"none\",\"valueType\":\"enum\"}]" });
				missingEnum.GetProperty("totalCount").GetInt64().ShouldBe(0);
				var sorted = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ IsApprovalView = true, MaxResultCount = 1, Sorting = "createdAt desc" });
				sorted.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(row.Id);
			}
		});
	}

	[Fact]
	public async Task Reservation_Page_Should_Filter_Search_Status_And_Page_Before_Enrichment()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (room, _) = await SeedRoomAndReservationAsync(actor);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "admin")))
			{
				for (var index = 0; index < 32; index++)
				{
					var purpose = index < 24 ? $"Match {index:00}" : index < 28 ? $"Else {index:00}" : $"Match approved {index:00}";
					var row = new RoomReservation(Guid.NewGuid(), null, room.Id, new DateTime(2030, 1, 7),
						new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), purpose, RoomReservationType.Meeting);
					if (index >= 28) row.Approve(actor);
					await _reservationRepository.InsertAsync(row, autoSave: true);
				}
				var db = await RoomDatabaseAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var page = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{
					SkipCount = 2, MaxResultCount = 5, StatusGroup = "pendingApproval", SearchTerm = "Match",
					SearchColumns = new[] { "purpose" }, Sorting = "purpose asc",
					Filters = "[{\"column\":\"purpose\",\"type\":\"startsWith\",\"value\":\"Match 0\",\"valueType\":\"string\"},{\"column\":\"purpose\",\"type\":\"equals\",\"value\":\"Match 12\",\"valueType\":\"string\"}]"
				});
				page.GetProperty("totalCount").GetInt64().ShouldBe(11);
				page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("purpose").GetString())
					.ShouldBe(Enumerable.Range(2, 5).Select(index => $"Match {index:00}"));
				page.GetProperty("statusCounts").GetProperty("all").GetInt32().ShouldBe(33);
				page.GetProperty("statusCounts").GetProperty("active").GetInt32().ShouldBe(4);
				capture.ReadsFor("RoomReservations").ShouldBeGreaterThan(0);
				capture.ReadsFor("RoomReservations").ShouldBeLessThanOrEqualTo(18);
				var hiddenColumn = await InvokeRoomPageAsync("GetReservationsPageAsync", new
				{ MaxResultCount = 5, SearchTerm = room.RoomName, SearchColumns = new[] { "purpose" } });
				hiddenColumn.GetProperty("totalCount").GetInt64().ShouldBe(0);
				db.ChangeTracker.Clear();
				var beforeSummary = capture.ReadsFor("RoomReservations");
				var summary = await InvokeRoomPageAsync("GetReservationSummaryAsync", new { });
				summary.GetProperty("statusCounts").GetProperty("all").GetInt32().ShouldBe(33);
				summary.GetProperty("statusCounts").GetProperty("pendingApproval").GetInt32().ShouldBe(29);
				(capture.ReadsFor("RoomReservations") - beforeSummary).ShouldBeLessThanOrEqualTo(12);
				db.ChangeTracker.Entries<RoomReservation>().ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task Audit_Page_Should_Bound_Full_Snapshots_Without_Removing_Inline_Diffs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (room, _) = await SeedRoomAndReservationAsync(actor);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "admin")))
			{
				var date = new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);
				for (var index = 0; index < 30; index++)
				{
					var row = new RoomAuditLog(Guid.NewGuid(), null, room.Id, null, "Room Reserved", actor, "Operator",
						$"before-{index}-{new string('a', 2048)}", $"after-{index}-{new string('b', 2048)}");
					typeof(RoomAuditLog).GetProperty(nameof(RoomAuditLog.Timestamp))!.SetValue(row, date.AddMinutes(-index));
					await GetRequiredService<IRepository<RoomAuditLog, Guid>>().InsertAsync(row, autoSave: true);
				}
				var db = await RoomDatabaseAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var page = await InvokeRoomPageAsync("GetAuditLogsPageAsync", new
				{ RoomId = room.Id, SkipCount = 3, MaxResultCount = 5, Action = "Room Reserved", FromDate = date.Date, ToDate = date.Date.AddDays(1).AddTicks(-1) });
				page.GetProperty("totalCount").GetInt64().ShouldBe(30);
				var items = page.GetProperty("items").EnumerateArray().ToArray();
				items.Length.ShouldBe(5);
				for (var index = 0; index < items.Length; index++)
				{
					items[index].GetProperty("oldValue").GetString().ShouldBe($"before-{index + 3}-{new string('a', 2048)}");
					items[index].GetProperty("newValue").GetString().ShouldBe($"after-{index + 3}-{new string('b', 2048)}");
					items[index].GetProperty("roomName").GetString().ShouldBe(room.RoomName);
				}
				capture.ReadsFor("RoomAuditLogs").ShouldBeGreaterThan(0);
				capture.ReadsFor("RoomAuditLogs").ShouldBeLessThanOrEqualTo(18);
				var matchingPage = await InvokeRoomPageAsync("GetAuditLogsPageAsync", new
				{ RoomId = room.Id, MaxResultCount = 5, SearchTerm = "AFTER-27-" });
				matchingPage.GetProperty("totalCount").GetInt64().ShouldBe(1);
				matchingPage.GetProperty("items").EnumerateArray().Single().GetProperty("newValue").GetString()
					.ShouldStartWith("after-27-");
			}
		});
	}

	[Fact]
	public async Task Availability_Master_Filters_Should_Precede_Room_Materialization()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (original, _) = await SeedRoomAndReservationAsync(actor);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "admin")))
			{
				for (var index = 0; index < 35; index++)
				{
					var room = new Room
					{
						RoomName = index == 0 ? "Eligible laboratory" : $"Small room {index}", Capacity = index == 0 ? 60 : 10,
						BuildingId = original.BuildingId, FloorId = original.FloorId, RoomTypeId = original.RoomTypeId
					};
					SetEntityId(room, Guid.NewGuid());
					await _roomRepository.InsertAsync(room, autoSave: true);
				}
				var db = await RoomDatabaseAsync();
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				var result = await _roomAvailabilityAppService.GetAvailabilityAsync(new RoomAvailabilityFilterDto
				{
					Date = new DateTime(2030, 1, 7), BuildingId = original.BuildingId, FloorId = original.FloorId,
					RoomTypeId = original.RoomTypeId, MinimumCapacity = 40, Search = "eligible", IncludeOccupied = true
				});
				result.ShouldHaveSingleItem().RoomName.ShouldBe("Eligible laboratory");
				capture.ReadsFor("Rooms").ShouldBeGreaterThan(0);
				capture.ReadsFor("Rooms").ShouldBeLessThanOrEqualTo(12);
				db.ChangeTracker.Entries<Room>().ShouldAllBe(entry => entry.Entity.Capacity >= 40);
			}
		});
	}

	[Fact]
	public async Task Recurring_Reservation_Should_Reuse_Range_Conflict_Reads_Without_Truncating_Occurrences()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (room, _) = await SeedRoomAndReservationAsync(actor);
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "admin")))
			{
				var db = await RoomDatabaseAsync();
				CreateRoomReservationDto Input(DateTime start, int days) => new()
				{
					RoomId = room.Id, Date = start, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 0, 0),
					Purpose = "Recurring range", ReservationType = RoomReservationType.Meeting, IsRecurring = true,
					RecurringConfig = new RecurringConfigDto { RepeatType = "daily", UntilDate = start.AddDays(days) }
				};
				int smallReads;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					await _roomAvailabilityAppService.CreateReservationAsync(Input(new DateTime(2030, 1, 7), 1));
					smallReads = small.CommandsFor("SELECT");
					smallReads.ShouldBeGreaterThan(0);
				}
				await db.SaveChangesAsync();
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				var result = await _roomAvailabilityAppService.CreateReservationAsync(Input(new DateTime(2030, 2, 4), 20));
				db.ChangeTracker.Entries<RoomReservation>().Count(entry => entry.Entity.RecurringConfigId == result.RecurringConfigId).ShouldBe(21);
				large.CommandsFor("SELECT").ShouldBeLessThanOrEqualTo(smallReads + 10);
			}
		});
	}

	private async Task<JsonElement> InvokeRoomPageAsync(string methodName, object input)
	{
		var method = typeof(IRoomAvailabilityAppService).GetMethod(methodName);
		method.ShouldNotBeNull("A parallel bounded endpoint must preserve the legacy array contract.");
		var parameter = JsonSerializer.Deserialize(JsonSerializer.Serialize(input, PageJson), method!.GetParameters().Single().ParameterType, PageJson);
		var task = (Task)method.Invoke(_roomAvailabilityAppService, [parameter])!;
		await task;
		var value = task.GetType().GetProperty("Result")!.GetValue(task);
		return JsonSerializer.SerializeToElement(value, PageJson);
	}
	private Task<ShikshaMitraDbContext> RoomDatabaseAsync() =>
		GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
}
