using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableClassReadPerformance_Tests : LessonPlanProgressTestBase
{
	[Theory]
	[InlineData(false, "en-US", "08:00", "09:00")]
	[InlineData(true, "fi-FI", "08.00", "09.00")]
	public async Task Class_read_preserves_complete_response_without_materializing_unrelated_catalogues(
		bool configured, string cultureName, string expectedStart, string expectedEnd)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedReadAsync(configured);
				using var culture = new ReadCulture(cultureName);
				var db = await DatabaseAsync();
				var service = GetRequiredService<ITimetableAppService>();
				db.ChangeTracker.Clear();
				string expected;
				int subjectRows, groupRows, roomRows, subjects, groups, rooms;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					var response = await service.GetByStandardAndDivisionAsync(StandardId, DivisionId);
					AssertResponse(response, fixture, configured, expectedStart, expectedEnd);
					expected = JsonSerializer.Serialize(response);
					subjectRows = small.ReadsFor("academic_Subjects");
					groupRows = small.ReadsFor("academic_ElectiveGroups");
					roomRows = small.ReadsFor("academic_Rooms");
					subjectRows.ShouldBeGreaterThan(0);
					groupRows.ShouldBeGreaterThan(0);
					roomRows.ShouldBeGreaterThan(0);
					subjects = db.ChangeTracker.Entries<Subject>().Count();
					groups = db.ChangeTracker.Entries<ElectiveGroup>().Count();
					rooms = db.ChangeTracker.Entries<Room>().Count();
				}
				for (var index = 0; index < 24; index++)
				{
					var unrelated = await AddSubjectAsync($"Unrelated {index}");
					var group = new ElectiveGroup(Guid.NewGuid(), TenantId, $"Unrelated group {index}", "#876543", AcademicMediumId);
					group.AddSubject(unrelated.Id);
					group.AddSubject(fixture.SelectedId);
					group.AddSubject(fixture.SyntheticId);
					await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
					await AddRoomAsync($"Unrelated room {index}", fixture.Room);
				}
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				var actual = await service.GetByStandardAndDivisionAsync(StandardId, DivisionId);
				JsonSerializer.Serialize(actual).ShouldBe(expected);
				AssertResponse(actual, fixture, configured, expectedStart, expectedEnd);
				Console.WriteLine($"Class timetable lookup rows subjects/groups/rooms: " +
					$"small={subjectRows}/{groupRows}/{roomRows}; unrelated catalogue={large.ReadsFor("academic_Subjects")}/{large.ReadsFor("academic_ElectiveGroups")}/{large.ReadsFor("academic_Rooms")}; " +
					$"tracked small={subjects}/{groups}/{rooms}; tracked unrelated={db.ChangeTracker.Entries<Subject>().Count()}/{db.ChangeTracker.Entries<ElectiveGroup>().Count()}/{db.ChangeTracker.Entries<Room>().Count()}");
				large.ReadsFor("academic_Subjects").ShouldBeLessThanOrEqualTo(subjectRows,
					"Unrelated tenant catalogue records must not increase subject rows read for one class timetable.");
				large.ReadsFor("academic_ElectiveGroups").ShouldBeLessThanOrEqualTo(groupRows);
				large.ReadsFor("academic_Rooms").ShouldBeLessThanOrEqualTo(roomRows);
				db.ChangeTracker.Entries<Subject>().Count().ShouldBe(subjects);
				db.ChangeTracker.Entries<ElectiveGroup>().Count().ShouldBe(groups);
				db.ChangeTracker.Entries<Room>().Count().ShouldBe(rooms);
			}
		});
	}

	[Fact]
	public async Task Draft_only_saved_timetable_keeps_lookup_labels_and_missing_reference_fallbacks()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedReadAsync(false);
				var assignments = await GetRequiredService<IRepository<AssignedSubject, Guid>>()
					.GetListAsync(assignment => assignment.StandardId == StandardId);
				foreach (var assignment in assignments) assignment.Status = SubjectStatus.Draft;
				await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateManyAsync(assignments, autoSave: true);
				(await DatabaseAsync()).ChangeTracker.Clear();
				var response = await GetRequiredService<ITimetableAppService>().GetByStandardAndDivisionAsync(StandardId, DivisionId);
				response.SubjectSummary.ShouldBeEmpty();
				response.TotalSlots.ShouldBe(4);
				response.OccupiedSlots.ShouldBe(4);
				var draft = response.Days["MONDAY"].ShouldHaveSingleItem().Subjects.ShouldHaveSingleItem();
				draft.Name.ShouldBe("Core Mathematics");
				draft.SubjectAssignmentId.ShouldBe(fixture.DraftAssignmentId);
				draft.RoomName.ShouldBe("Saved classroom");
				var selected = response.Days["TUESDAY"].ShouldHaveSingleItem().Subjects.ShouldHaveSingleItem();
				selected.Name.ShouldBe("Archived selected");
				selected.SubjectAssignmentId.ShouldBe(fixture.SelectedAssignmentId);
				selected.TotalSlots.ShouldBe(0);
				selected.OccupiedSlots.ShouldBe(1);
				selected.UnoccupiedSlots.ShouldBe(-1);
				AssertSyntheticAndMissing(response, fixture);
			}
		});
	}

	private void AssertResponse(TimetableResponseDto response, ReadFixture fixture, bool configured,
		string expectedStart, string expectedEnd)
	{
		response.Days.Keys.ShouldBe(new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" });
		response.TotalSlots.ShouldBe(configured ? 6 : 4);
		response.OccupiedSlots.ShouldBe(4);
		response.UnoccupiedSlots.ShouldBe(configured ? 2 : 0);
		var monday = response.Days["MONDAY"].ShouldHaveSingleItem();
		monday.Sequence.ShouldBe(configured ? 1 : 7);
		monday.StartTime.ShouldBe(expectedStart);
		monday.EndTime.ShouldBe(expectedEnd);
		monday.Type.ShouldBe(SlotType.Period);
		var draft = monday.Subjects.ShouldHaveSingleItem();
		draft.SubjectId.ShouldBe(SubjectId);
		draft.SubjectAssignmentId.ShouldBe(fixture.DraftAssignmentId);
		draft.Name.ShouldBe("Core Mathematics");
		draft.Color.ShouldBe("#112233");
		draft.TeacherId.ShouldBe(TeacherEmployeeId);
		draft.TeacherName.ShouldNotBeNullOrWhiteSpace();
		draft.RoomId.ShouldBe(fixture.Room.Id);
		draft.RoomName.ShouldBe("Saved classroom");
		draft.TotalSlots.ShouldBe(0);
		draft.OccupiedSlots.ShouldBe(2);
		draft.UnoccupiedSlots.ShouldBe(-2);
		var tuesday = response.Days["TUESDAY"].ShouldHaveSingleItem();
		tuesday.ElectiveGroupId.ShouldBe(fixture.GroupId);
		tuesday.ElectiveGroupName.ShouldBe("Archived selected");
		tuesday.ElectiveGroupColor.ShouldBe("#123456");
		var selected = tuesday.Subjects.ShouldHaveSingleItem();
		selected.SubjectId.ShouldBe(fixture.SelectedId);
		selected.Name.ShouldBe("Archived selected");
		selected.Color.ShouldBe("#224466");
		selected.SubjectAssignmentId.ShouldBe(fixture.SelectedAssignmentId);
		selected.TeacherId.ShouldBeNull();
		selected.RoomId.ShouldBeNull();
		selected.TotalSlots.ShouldBe(3);
		selected.OccupiedSlots.ShouldBe(1);
		selected.UnoccupiedSlots.ShouldBe(2);
		AssertSyntheticAndMissing(response, fixture);
		response.SubjectSummary.Count.ShouldBe(2);
		response.SubjectSummary.Select(item => item.SubjectType).ShouldBe(new[] { SubjectType.Core, SubjectType.Elective });
		var core = response.SubjectSummary[0];
		core.Subjects.ShouldHaveSingleItem().SubjectName.ShouldBe("Summary only");
		core.TotalSlots.ShouldBe(4);
		core.OccupiedSlots.ShouldBe(0);
		core.UnoccupiedSlots.ShouldBe(4);
		var elective = response.SubjectSummary[1];
		elective.ElectiveGroupName.ShouldBe("Archived selected");
		elective.Subjects.ShouldHaveSingleItem().SubjectId.ShouldBe(fixture.SelectedId);
		elective.TotalSlots.ShouldBe(3);
		elective.OccupiedSlots.ShouldBe(1);
		elective.UnoccupiedSlots.ShouldBe(2);
		foreach (var day in new[] { "FRIDAY", "SATURDAY" })
		{
			if (configured)
			{
				var placeholder = response.Days[day].ShouldHaveSingleItem();
				placeholder.Subjects.ShouldBeEmpty();
				placeholder.SubjectType.ShouldBeNull();
			}
			else response.Days[day].ShouldBeEmpty();
		}
	}

	private static void AssertSyntheticAndMissing(TimetableResponseDto response, ReadFixture fixture)
	{
		var synthetic = response.Days["WEDNESDAY"].ShouldHaveSingleItem().Subjects.ShouldHaveSingleItem();
		synthetic.SubjectId.ShouldBe(fixture.SyntheticId);
		synthetic.Name.ShouldBe("Selected without allocation");
		synthetic.SubjectAssignmentId.ShouldBe(Guid.Empty);
		synthetic.TeacherId.ShouldBeNull();
		synthetic.RoomId.ShouldBeNull();
		var missing = response.Days["THURSDAY"].ShouldHaveSingleItem().Subjects.ShouldHaveSingleItem();
		missing.SubjectId.ShouldBe(fixture.MissingId);
		missing.Name.ShouldBeEmpty();
		missing.Color.ShouldBeNull();
		missing.SubjectAssignmentId.ShouldBe(Guid.Empty);
		missing.RoomId.ShouldBe(fixture.MissingId);
		missing.RoomName.ShouldBeNull();
		missing.TeacherName.ShouldBeNull();
		missing.TotalSlots.ShouldBe(0);
		missing.OccupiedSlots.ShouldBe(1);
		missing.UnoccupiedSlots.ShouldBe(-1);
	}

	private async Task<ReadFixture> SeedReadAsync(bool configured)
	{
		await SeedContextAsync();
		await SeedLessonPlanOwnerAsync();
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId), autoSave: true);
		var original = (await GetRequiredService<IRepository<AssignedSubject, Guid>>()
			.GetListAsync(assignment => assignment.StandardId == StandardId)).Single();
		var previous = await AddSubjectAsync("Previous selection");
		var selected = await AddSubjectAsync("Archived selected", SubjectMasterStatus.Archived);
		var synthetic = await AddSubjectAsync("Selected without allocation", SubjectMasterStatus.Inactive);
		var summary = await AddSubjectAsync("Summary only");
		var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Archived group", "#123456", AcademicMediumId,
			status: SubjectMasterStatus.Archived);
		group.AddSubject(previous.Id);
		group.AddSubject(selected.Id);
		await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
		var syntheticGroup = new ElectiveGroup(Guid.NewGuid(), TenantId, "Draft saved group", "#654321", AcademicMediumId);
		syntheticGroup.AddSubject(SubjectId);
		syntheticGroup.AddSubject(synthetic.Id);
		await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(syntheticGroup, autoSave: true);
		await AddAssignmentAsync(previous.Id, group.Id);
		var selectedAssignment = await AddAssignmentAsync(selected.Id, group.Id);
		await AddAssignmentAsync(summary.Id, null);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
		var student = await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(
			new Student(Guid.NewGuid(), TenantId, "Timetable", null, "Student", board.AcademicYearId,
				board.Id, medium.Id, standard.EducationMediumStageId, StandardId, DivisionId), autoSave: true);
		foreach (var (groupId, subjectId) in new[] { (group.Id, selected.Id), (syntheticGroup.Id, synthetic.Id) })
			await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().InsertAsync(
				new StudentElectiveSelection(Guid.NewGuid(), TenantId, student.Id, groupId, subjectId, medium.Id), autoSave: true);
		var room = await SeedRoomAsync();
		var missing = Guid.NewGuid();
		var timetable = new Timetables.Timetable(Guid.NewGuid(), TenantId, StandardId, DivisionId);
		AddSlot(timetable, TimetableDayOfWeek.Monday, 7, SubjectId, null, TeacherEmployeeId, room.Id);
		AddSlot(timetable, TimetableDayOfWeek.Tuesday, 3, previous.Id, group.Id, TeacherEmployeeId, room.Id);
		AddSlot(timetable, TimetableDayOfWeek.Wednesday, 4, SubjectId, syntheticGroup.Id, TeacherEmployeeId, room.Id);
		AddSlot(timetable, TimetableDayOfWeek.Thursday, 9, missing, null, missing, missing);
		await GetRequiredService<ITimetableRepository>().InsertAsync(timetable, autoSave: true);
		var weekly = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, StandardId);
		weekly.AddSubject(Guid.NewGuid(), summary.Id, null, 4);
		weekly.AddSubject(Guid.NewGuid(), null, group.Id, 3);
		foreach (var entry in weekly.Subjects)
			typeof(WeeklyLectureSlotSubject).GetProperty(nameof(WeeklyLectureSlotSubject.TenantId))!.SetValue(entry, TenantId);
		await GetRequiredService<IWeeklyLectureSlotRepository>().InsertAsync(weekly, autoSave: true);
		if (configured)
		{
			var config = new LectureSlotConfig(Guid.NewGuid(), TenantId, StandardId, "08:00-09:00");
			var days = new LectureSlotGroup(Guid.NewGuid(), config.Id, 1, 0, 5, [0, 1, 2, 3, 4, 5]);
			typeof(LectureSlotGroup).GetProperty(nameof(LectureSlotGroup.TenantId))!.SetValue(days, TenantId);
			days.AddTimeSlot(new LectureSlotTimeSlot(Guid.NewGuid(), days.Id, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotType.Period));
			config.AddSlotGroup(days);
			await GetRequiredService<ILectureSlotConfigRepository>().InsertAsync(config, autoSave: true);
		}
		return new ReadFixture(original.Id, selectedAssignment.Id, selected.Id, synthetic.Id, group.Id, missing, room);
	}

	private async Task<Subject> AddSubjectAsync(string name, SubjectMasterStatus status = SubjectMasterStatus.Active) =>
		await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(
			new Subject(Guid.NewGuid(), TenantId, name, "#224466", AcademicMediumId, status: status), autoSave: true);
	private async Task<AssignedSubject> AddAssignmentAsync(Guid subjectId, Guid? groupId) =>
		await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(
			new AssignedSubject(Guid.NewGuid(), TenantId, StandardId, subjectId, "READ")
			{ Status = SubjectStatus.Published, ElectiveGroupId = groupId, SubjectType = groupId.HasValue ? SubjectType.Elective : SubjectType.Core }, autoSave: true);
	private static void AddSlot(Timetables.Timetable timetable, TimetableDayOfWeek day, int sequence,
		Guid subjectId, Guid? groupId, Guid? teacherId, Guid? roomId)
	{
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, sequence, new TimeOnly(8, 0), new TimeOnly(9, 0),
			SlotType.Period, subjectType: groupId.HasValue ? SubjectType.Elective : SubjectType.Core, electiveGroupId: groupId);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, subjectId, teacherId, roomId));
		timetable.AddSlot(slot);
	}
	private async Task<Room> SeedRoomAsync()
	{
		var building = new Building { TenantId = TenantId, BuildingName = "Read building", TotalFloors = 1 };
		SetId(building);
		await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, autoSave: true);
		var floor = new Floor { BuildingId = building.Id, FloorName = "Ground", FloorNo = 0 };
		SetId(floor);
		await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, autoSave: true);
		var type = new RoomType { TenantId = TenantId, RoomTypeName = "Class", ShortName = "CL" };
		SetId(type);
		await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(type, autoSave: true);
		return await AddRoomAsync("Saved classroom", new Room { BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id });
	}
	private async Task<Room> AddRoomAsync(string name, Room parent)
	{
		var room = new Room { TenantId = TenantId, RoomName = name, Capacity = 30,
			BuildingId = parent.BuildingId, FloorId = parent.FloorId, RoomTypeId = parent.RoomTypeId };
		SetId(room);
		return await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, autoSave: true);
	}
	private static void SetId<TEntity>(TEntity entity) where TEntity : Entity<Guid> =>
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());
	private Task<ShikshaMitraDbContext> DatabaseAsync() =>
		GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
	private sealed record ReadFixture(Guid DraftAssignmentId, Guid SelectedAssignmentId, Guid SelectedId,
		Guid SyntheticId, Guid GroupId, Guid MissingId, Room Room);
	private sealed class ReadCulture : IDisposable
	{
		private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
		private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
		public ReadCulture(string name)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
		}
		public void Dispose()
		{
			CultureInfo.CurrentCulture = _culture;
			CultureInfo.CurrentUICulture = _uiCulture;
		}
	}
}
