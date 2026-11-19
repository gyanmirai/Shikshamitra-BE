using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableSettingsReadPerformance_Tests : LessonPlanProgressTestBase
{
	[Fact]
	public async Task Management_Page_Should_Not_Hydrate_Off_Page_Division_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
					new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId), autoSave: true);
				var division = await GetRequiredService<DivisionManager>().CreateAsync(StandardId, "A", 30);
				await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
				var repository = GetRequiredService<ITimetableRepository>();
				var displayed = MakeTimetable(division.Id, TimetableDayOfWeek.Wednesday);
				var outside = MakeTimetable(DivisionId, TimetableDayOfWeek.Thursday);
				await repository.InsertAsync(displayed, autoSave: true);
				await repository.InsertAsync(outside, autoSave: true);
				var db = await DatabaseAsync();
				db.ChangeTracker.Clear();
				var service = GetRequiredService<ITimetableAppService>();
				var input = new GetTimetableManagementInput { MaxResultCount = 1 };
				int smallReads;
				string expected;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					var page = await service.GetManagementByStandardAsync(StandardId, input);
					page.TotalCount.ShouldBe(2);
					page.Items.ShouldHaveSingleItem().DivisionId.ShouldBe(division.Id);
					expected = JsonSerializer.Serialize(page);
					smallReads = small.ReadsFor("Timetables");
					smallReads.ShouldBeGreaterThan(0);
				}
				outside = await repository.GetAsync(outside.Id);
				for (var index = 0; index < 40; index++) AddTimetableSlot(outside, TimetableDayOfWeek.Thursday, index + 10);
				await repository.UpdateAsync(outside, autoSave: true);
				db.ChangeTracker.Clear();
				using var large = new TimetableReadCapture(db.Database.GetDbConnection());
				JsonSerializer.Serialize(await service.GetManagementByStandardAsync(StandardId, input)).ShouldBe(expected);
				large.ReadsFor("Timetables").ShouldBeLessThanOrEqualTo(smallReads + 5);
				db.ChangeTracker.Entries<Timetables.Timetable>().ShouldAllBe(entry => entry.Entity.DivisionId == division.Id);
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Settings_Lists_Should_Hydrate_Only_Requested_Page_Graphs(bool weekly)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				var standards = await SeedSettingsClassesAsync(18);
				foreach (var standard in standards)
				{
					await GetRequiredService<ILectureSlotConfigRepository>().InsertAsync(MakeConfig(standard.Id), autoSave: true);
					var assigned = new AssignedSubject(Guid.NewGuid(), TenantId, standard.Id, SubjectId, "CORE") { Status = SubjectStatus.Published };
					await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(assigned, autoSave: true);
					var weeklySlot = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, standard.Id);
					weeklySlot.AddSubject(Guid.NewGuid(), SubjectId, null, 3);
					typeof(WeeklyLectureSlotSubject).GetProperty(nameof(WeeklyLectureSlotSubject.TenantId))!
						.SetValue(weeklySlot.Subjects.Single(), TenantId);
					await GetRequiredService<IWeeklyLectureSlotRepository>().InsertAsync(weeklySlot, autoSave: true);
				}
				var db = await DatabaseAsync();
				db.ChangeTracker.Clear();
				var expectedNames = standards.OrderBy(standard => standard.Name).Skip(2).Take(3).Select(standard => standard.Name).ToArray();
				if (weekly)
				{
					var page = await GetRequiredService<IWeeklyLectureSlotAppService>().GetListAsync(new GetWeeklyLectureSlotsInput
					{ AcademicMediumId = AcademicMediumId, SkipCount = 2, MaxResultCount = 3 });
					page.TotalCount.ShouldBe(18);
					page.Items.Select(item => item.StandardName).ShouldBe(expectedNames);
					page.Items.ShouldAllBe(item => item.TotalLectures == 3 && item.AvailableTeachingCapacity == 5 && item.Difference == 2);
					db.ChangeTracker.Entries<WeeklyLectureSlot>().Count().ShouldBeLessThanOrEqualTo(3);
				}
				else
				{
					var page = await GetRequiredService<ILectureSlotAppService>().GetByMediumAsync(new GetLectureSlotsByMediumInput
					{ AcademicMediumId = AcademicMediumId, SkipCount = 2, MaxResultCount = 3 });
					page.TotalCount.ShouldBe(18);
					page.Items.Select(item => item.StandardName).ShouldBe(expectedNames);
					page.Items.ShouldAllBe(item => item.WeeklyTeachingCapacity == 5 && item.RequiredWeeklyLectures == 3 && item.Difference == 2);
				}
				db.ChangeTracker.Entries<LectureSlotConfig>().Count().ShouldBeLessThanOrEqualTo(3);
			}
		});
	}

	[Fact]
	public async Task Multi_Class_Assignment_Should_Batch_Reads_And_Preserve_Cloned_Configuration()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				var standards = await SeedSettingsClassesAsync(10);
				var source = MakeConfig(standards[0].Id);
				var repository = GetRequiredService<ILectureSlotConfigRepository>();
				await repository.InsertAsync(source, autoSave: true);
				var db = await DatabaseAsync();
				db.ChangeTracker.Clear();
				var service = GetRequiredService<ILectureSlotAppService>();
				int smallReads;
				using (var small = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					await service.AssignAsync(source.Id, new AssignLectureSlotDto { SelectedStandardIds = [standards[1].Id] });
					smallReads = small.CommandsFor("SELECT");
					smallReads.ShouldBeGreaterThan(0);
				}
				await db.SaveChangesAsync();
				db.ChangeTracker.Clear();
				var targets = standards.Skip(2).Select(standard => standard.Id).ToList();
				int largeReads;
				string readDiagnostics;
				using (var large = new TimetableReadCapture(db.Database.GetDbConnection()))
				{
					await service.AssignAsync(source.Id, new AssignLectureSlotDto { SelectedStandardIds = targets });
					largeReads = large.CommandsFor("SELECT");
					readDiagnostics = string.Join(Environment.NewLine, large.SelectStatements);
				}
				largeReads.ShouldBeLessThanOrEqualTo(smallReads + 3, readDiagnostics);
				await db.SaveChangesAsync();
				var query = await repository.GetQueryableAsync();
				var copies = await query.Where(config => targets.Contains(config.StandardId)).ToListAsync();
				copies.Count.ShouldBe(8);
				copies.ShouldAllBe(config => config.SchoolHours == source.SchoolHours && config.PreviousStandardId == source.PreviousStandardId);
			}
		});
	}

	private async Task<List<Standard>> SeedSettingsClassesAsync(int count)
	{
		var original = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
		var shift = new ShiftManagement(Guid.NewGuid(), TenantId, "Settings shift", new TimeOnly(8, 0), new TimeOnly(16, 0));
		await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(shift, autoSave: true);
		var standards = new List<Standard>();
		for (var index = 0; index < count; index++)
		{
			var standard = new Standard(Guid.NewGuid(), TenantId, $"Settings {index:00}", 6, original.EducationMediumStageId, shift.Id);
			await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, autoSave: true);
			standards.Add(standard);
		}
		return standards;
	}
	private static LectureSlotConfig MakeConfig(Guid standardId)
	{
		var config = new LectureSlotConfig(Guid.NewGuid(), TenantId, standardId, "08:00-09:00");
		var group = new LectureSlotGroup(Guid.NewGuid(), config.Id, 1, 0, 4, [0, 1, 2, 3, 4]);
		typeof(LectureSlotGroup).GetProperty(nameof(LectureSlotGroup.TenantId))!.SetValue(group, TenantId);
		group.AddTimeSlot(new LectureSlotTimeSlot(Guid.NewGuid(), group.Id, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotType.Period));
		config.AddSlotGroup(group);
		return config;
	}
	private Timetables.Timetable MakeTimetable(Guid divisionId, TimetableDayOfWeek day)
	{
		var timetable = new Timetables.Timetable(Guid.NewGuid(), TenantId, StandardId, divisionId);
		AddTimetableSlot(timetable, day, 1);
		timetable.Publish();
		return timetable;
	}
	private void AddTimetableSlot(Timetables.Timetable timetable, TimetableDayOfWeek day, int sequence)
	{
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, sequence, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotType.Period);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, SubjectId, TeacherEmployeeId));
		timetable.AddSlot(slot);
	}
	private Task<ShikshaMitraDbContext> DatabaseAsync() =>
		GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
}
