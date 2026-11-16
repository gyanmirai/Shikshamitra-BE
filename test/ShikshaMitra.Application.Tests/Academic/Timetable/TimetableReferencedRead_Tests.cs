using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public partial class TimetableClassReadPerformance_Tests
{
	[Fact]
	public async Task Lecture_lookup_keeps_user_id_teacher_resolution_and_missing_reference_fallbacks()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedReadAsync(false);
				var repository = GetRequiredService<ITimetableRepository>();
				var timetable = (await repository.FindByStandardAndDivisionAsync(StandardId, DivisionId))!;
				var monday = timetable.Slots.Single(slot => slot.Day == TimetableDayOfWeek.Monday).Subjects.Single();
				typeof(TimetableSlotSubject).GetProperty(nameof(TimetableSlotSubject.TeacherId))!.SetValue(monday, TeacherUserId);
				await repository.UpdateAsync(timetable, autoSave: true);
				(await DatabaseAsync()).ChangeTracker.Clear();
				var service = GetRequiredService<ISubstitutionAppService>();
				var found = await service.GetLecturesAsync(new SubstitutionLectureLookupInput
				{ StandardId = StandardId, DivisionId = DivisionId, Date = new DateTimeOffset(2030, 1, 7, 0, 0, 0, TimeSpan.Zero) });
				found.ShouldHaveSingleItem().OriginalTeacherId.ShouldBe(TeacherEmployeeId);
				found[0].OriginalTeacherName.ShouldNotBeNullOrWhiteSpace();
				var missing = await service.GetLecturesAsync(new SubstitutionLectureLookupInput
				{ StandardId = StandardId, DivisionId = DivisionId, Date = new DateTimeOffset(2030, 1, 10, 0, 0, 0, TimeSpan.Zero) });
				missing.ShouldHaveSingleItem().OriginalTeacherId.ShouldBe(fixture.MissingId);
				missing[0].OriginalTeacherName.ShouldBeEmpty();
				missing[0].SubjectName.ShouldBeEmpty();
				missing[0].RoomName.ShouldBeEmpty();
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Referenced_detail_reads_do_not_grow_with_unrelated_catalogues(bool weekly)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedReadAsync(false);
				var slot = (await GetRequiredService<IWeeklyLectureSlotRepository>().GetListAsync()).Single();
				var db = await DatabaseAsync();
				async Task<string> ReadAsync()
				{
					if (weekly)
					{
						var result = await GetRequiredService<IWeeklyLectureSlotAppService>().GetAsync(slot.Id);
						result.TotalLectures.ShouldBe(7);
						var group = result.Subjects.Single(subject => subject.ElectiveGroupId == fixture.GroupId);
						group.ElectiveGroupName.ShouldBe("Archived group");
						group.ElectiveGroupSubjects.Select(subject => subject.Name).ShouldBe(new[] { "Previous selection", "Archived selected" });
						return JsonSerializer.Serialize(result);
					}
					var lectures = await GetRequiredService<ISubstitutionAppService>().GetLecturesAsync(new SubstitutionLectureLookupInput
					{ StandardId = StandardId, DivisionId = DivisionId, Date = new DateTimeOffset(2030, 1, 7, 0, 0, 0, TimeSpan.Zero) });
					var lecture = lectures.ShouldHaveSingleItem();
					lecture.OriginalTeacherId.ShouldBe(TeacherEmployeeId);
					lecture.OriginalTeacherName.ShouldNotBeNullOrWhiteSpace();
					lecture.RoomName.ShouldBe("Saved classroom");
					lecture.SubjectName.ShouldBe("Core Mathematics");
					return JsonSerializer.Serialize(lectures);
				}
				db.ChangeTracker.Clear();
				string expected;
				int subjectRows, groupRows, roomRows, employeeRows;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					expected = await ReadAsync();
					subjectRows = small.ReadsFor("academic_Subjects");
					groupRows = small.ReadsFor("academic_ElectiveGroups");
					roomRows = small.ReadsFor("academic_Rooms");
					employeeRows = small.ReadsFor("Employees");
				}
				var employeeRoleId = (await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(TeacherEmployeeId)).RoleId;
				for (var index = 0; index < 12; index++)
				{
					var subject = await AddSubjectAsync($"Unreferenced {index}");
					var group = new ElectiveGroup(Guid.NewGuid(), TenantId, $"Unreferenced {index}", "#123456", AcademicMediumId);
					group.AddSubject(subject.Id);
					await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
					await AddRoomAsync($"Unreferenced {index}", fixture.Room);
					await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(new Employee(
						Guid.NewGuid(), TenantId, null, $"UNREF{index}",
						new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Employee", FirstName: $"Unreferenced {index}",
							MiddleName: null, ShortName: null, EmployeeStatusType: null, DateOfBirth: null, FamilyCast: null,
							CastType: null, GenderType: null, Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: null,
							SecondaryMobileNo: null, Email: null, PanNo: $"UNREF{index:D4}F", AdharNo: $"12345678{index:D4}",
							Address1: null, Address2: null, Address3: null, Department: null, Designation: null, DateOfJoining: null,
							EmployeeGroup: null, Qualification: null, RoleId: employeeRoleId, IdentificationMark1: null,
							City: null, State: null, Country: null, PinCode: null, ShiftId: null), null, null), autoSave: true);
				}
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				(await ReadAsync()).ShouldBe(expected);
				Console.WriteLine($"Referenced timetable detail weekly={weekly}: subjects/groups/rooms/employees " +
					$"small={subjectRows}/{groupRows}/{roomRows}/{employeeRows}; unrelated catalogue=" +
					$"{large.ReadsFor("academic_Subjects")}/{large.ReadsFor("academic_ElectiveGroups")}/{large.ReadsFor("academic_Rooms")}/{large.ReadsFor("Employees")}");
				large.ReadsFor("academic_Subjects").ShouldBeLessThanOrEqualTo(subjectRows);
				large.ReadsFor("academic_ElectiveGroups").ShouldBeLessThanOrEqualTo(groupRows);
				large.ReadsFor("academic_Rooms").ShouldBeLessThanOrEqualTo(roomRows);
				large.ReadsFor("Employees").ShouldBeLessThanOrEqualTo(employeeRows);
				db.ChangeTracker.Entries<Subject>().ShouldBeEmpty();
				if (!weekly) db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task Room_assignment_conflicts_use_published_cross_context_rows_without_hydrating_graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedReadAsync(false);
				var repository = GetRequiredService<ITimetableRepository>();
				var other = new Timetables.Timetable(Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid());
				AddSlot(other, TimetableDayOfWeek.Monday, 1, SubjectId, null, null, fixture.Room.Id);
				other.Publish();
				await repository.InsertAsync(other, autoSave: true);
				var currentSlot = other.Slots.Single().Id;
				var service = GetRequiredService<SubstitutionAppService>();
				var method = typeof(SubstitutionAppService).GetMethod("EnsureAssignedRoomAvailableAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
				Task Check(Guid? excludedSlot, int start = 8, int end = 9) => (Task)method.Invoke(service,
					new object?[] { new DateTime(2030, 1, 7), new TimeOnly(start, 0), new TimeOnly(end, 0), fixture.Room.Id, excludedSlot, null })!;
				var db = await DatabaseAsync();
				db.ChangeTracker.Clear();
				var persisted = await repository.GetListAsync(timetable => timetable.Id == other.Id, includeDetails: true);
				persisted.ShouldHaveSingleItem().Slots.ShouldHaveSingleItem().Subjects.ShouldHaveSingleItem().RoomId.ShouldBe(fixture.Room.Id);
				db.ChangeTracker.Clear();
				using var capture = new TimetableReadCapture(db.Database.GetDbConnection());
				(await Should.ThrowAsync<UserFriendlyException>(() => Check(null))).Message.ShouldContain("published timetable");
				await Check(currentSlot);
				await Check(null, 9, 10);
				db.ChangeTracker.Entries<Timetables.Timetable>().ShouldBeEmpty();
				db.ChangeTracker.Entries<TimetableSlot>().ShouldBeEmpty();
				await repository.DeleteAsync(other.Id, autoSave: true);
				db.ChangeTracker.Clear();
				await Check(null);
				var otherTenantId = Guid.NewGuid();
				using (Tenant.Change(otherTenantId))
				{
					var hidden = new Timetables.Timetable(Guid.NewGuid(), otherTenantId, Guid.NewGuid(), Guid.NewGuid());
					AddSlot(hidden, TimetableDayOfWeek.Monday, 1, SubjectId, null, null, fixture.Room.Id);
					hidden.Publish();
					await repository.InsertAsync(hidden, autoSave: true);
				}
				db.ChangeTracker.Clear();
				await Check(null);
			}
		});
	}
}
