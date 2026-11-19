using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class TeacherAvailabilityAppService_Tests
{
	[Fact]
	public async Task Availability_Page_Should_Share_Affected_Lecture_Reads_Across_Its_Rows()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
			{
				await GrantCurrentActorScopeAsync(tenantId);
				var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Page", "Teacher", tenantId: tenantId);
				var context = await SeedTimetableContextAsync(tenantId, teacher.Id, "Page coverage year",
					new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), "Page coverage class", new TimeOnly(8, 0), new TimeOnly(8, 45));
				var date = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
				for (var index = 0; index < 8; index++)
					await _availabilityRepository.InsertAsync(new TeacherAvailability(Guid.NewGuid(), tenantId,
						teacher.EmployeeName, date, new TimeOnly(8, 0), new TimeOnly(9, 0), TeacherAvailabilityStatus.Unavailable,
						TeacherAvailabilitySource.Manual, "Page coverage", false, false, context.AcademicYearId, teacher.Id,
						affectedLectureCount: 1), autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				int smallCommands;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					var page = await _teacherAvailabilityAppService.GetListAsync(new GetTeacherAvailabilityInput
					{ Date = date, AcademicYearId = context.AcademicYearId, MaxResultCount = 1 });
					page.Items.ShouldHaveSingleItem().AffectedLectureCount.ShouldBe(1);
					smallCommands = small.Count;
					smallCommands.ShouldBeGreaterThan(0);
				}
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				var result = await _teacherAvailabilityAppService.GetListAsync(new GetTeacherAvailabilityInput
				{ Date = date, AcademicYearId = context.AcademicYearId, MaxResultCount = 10 });
				result.TotalCount.ShouldBe(8);
				result.Items.ShouldAllBe(item => item.AffectedLectureCount == 1 && item.CoveredLectureCount == 0 && item.UncoveredLectureCount == 1);
				large.Count.ShouldBeLessThanOrEqualTo(smallCommands + 2,
					"Affected-lecture context should be loaded once for the selected page.");
				db.ChangeTracker.Entries<Timetables.Timetable>().ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task Suggestions_Should_Project_Only_Relevant_Day_Time_Slots_And_Preserve_Ranking()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = Guid.NewGuid();
			using (GetRequiredService<ICurrentTenant>().Change(tenantId))
			using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
			{
				await GrantCurrentActorScopeAsync(tenantId);
				var teacher = await CreateEmployeeAsync(Guid.NewGuid(), "Busy", "Teacher", tenantId: tenantId);
				await CreateEmployeeAsync(Guid.NewGuid(), "Free", "Teacher", tenantId: tenantId);
				var context = await SeedTimetableContextAsync(tenantId, teacher.Id, "Suggestion year",
					new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), "Suggestion class", new TimeOnly(8, 0), new TimeOnly(8, 45));
				var service = GetRequiredService<ISubstitutionAppService>();
				var input = new SuggestedTeacherInput
				{ Date = new DateTimeOffset(new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc)), StartTime = "08:00", EndTime = "09:00" };
				var warm = await service.GetSuggestedTeachersAsync(input);
				var occupied = warm.Single(item => item.TeacherId == teacher.Id);
				occupied.IsPartialCoverage.ShouldBeTrue();
				occupied.CoverageStartTime.ShouldBe("08:45");
				occupied.CoverageEndTime.ShouldBe("09:00");
				occupied.CoverageMinutes.ShouldBe(15);
				occupied.Score.ShouldBe(55);
				var expected = JsonSerializer.Serialize(warm);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				int smallReads;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					JsonSerializer.Serialize(await service.GetSuggestedTeachersAsync(input)).ShouldBe(expected);
					smallReads = small.ReadsFor("Timetables");
					smallReads.ShouldBeGreaterThan(0);
				}
				var repository = GetRequiredService<ITimetableRepository>();
				var timetable = (await repository.GetByStandardIdAsync(context.StandardId)).Single();
				for (var index = 0; index < 40; index++)
				{
					var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Thursday, index + 10,
						new TimeOnly(10, 0).AddMinutes(index), new TimeOnly(10, 0).AddMinutes(index + 1), SlotType.Period);
					slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, context.SubjectId, teacher.Id));
					timetable.AddSlot(slot);
				}
				await repository.UpdateAsync(timetable, autoSave: true);
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				JsonSerializer.Serialize(await service.GetSuggestedTeachersAsync(input)).ShouldBe(expected);
				large.ReadsFor("Timetables").ShouldBeLessThanOrEqualTo(smallReads + 5);
				db.ChangeTracker.Entries<Timetables.Timetable>().ShouldBeEmpty();
			}
		});
	}
}
