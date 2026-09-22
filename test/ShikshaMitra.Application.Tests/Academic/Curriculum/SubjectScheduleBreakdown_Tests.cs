using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using TimetableAggregate = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectScheduleBreakdown_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Should_Return_The_Saved_Day_Period_Division_And_Teacher()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var weeklySlot = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, StandardId);
			weeklySlot.AddSubject(Guid.NewGuid(), SubjectId, null, 6);
			await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>()
				.InsertAsync(weeklySlot, autoSave: true);
			await AddTimetableAsync();

			var result = await Allocations.GetScheduleBreakdownAsync(AssignmentId);

			result.AllocatedPeriodsPerWeek.ShouldBe(6);
			result.ScheduledPeriods.ShouldBe(1);
			result.DivisionCount.ShouldBe(1);
			var lecture = result.Lectures.Single();
			lecture.DivisionName.ShouldBe("B");
			lecture.Day.ShouldBe(TimetableDayOfWeek.Tuesday);
			lecture.PeriodNumber.ShouldBe(5);
			lecture.StartTime.ShouldBe("09:00");
			lecture.EndTime.ShouldBe("09:30");
			lecture.TimetableStatus.ShouldBe(TimetableStatus.Published);
			var subject = lecture.Subjects.Single();
			subject.SubjectName.ShouldBe("Core Mathematics");
			subject.TeacherName.ShouldBe("Old Teacher");
		});
	}

	[Fact]
	public async Task Should_Group_Elective_Subjects_Sharing_The_Same_Lecture()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Languages", "#7c3aed", AcademicMediumId);
			await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
			var subjects = new[]
			{
				new Subject(Guid.NewGuid(), TenantId, "French", "#2563eb", AcademicMediumId),
				new Subject(Guid.NewGuid(), TenantId, "German", "#ea580c", AcademicMediumId)
			};
			await GetRequiredService<IRepository<Subject, Guid>>().InsertManyAsync(subjects, autoSave: true);
			var groupedAssignments = subjects.Select(subject => new AssignedSubject(
				Guid.NewGuid(), TenantId, StandardId, subject.Id, subject.Name,
				subjectType: SubjectType.Elective,
				electiveGroupId: group.Id,
				weeklyPeriods: 4)).ToList();
			await GetRequiredService<IRepository<AssignedSubject, Guid>>()
				.InsertManyAsync(groupedAssignments, autoSave: true);
			var weeklySlot = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, StandardId);
			weeklySlot.AddSubject(Guid.NewGuid(), null, group.Id, 4);
			await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>()
				.InsertAsync(weeklySlot, autoSave: true);

			var timetable = new TimetableAggregate(Guid.NewGuid(), TenantId, StandardId, DivisionId);
			var slot = new TimetableSlot(
				Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 2,
				new TimeOnly(10, 0), new TimeOnly(10, 30), SlotType.Period,
				subjectType: SubjectType.Elective,
				electiveGroupId: group.Id);
			foreach (var subject in subjects)
			{
				slot.AddSubject(new TimetableSlotSubject(
					Guid.NewGuid(), slot.Id, subject.Id, OldTeacher));
			}
			timetable.AddSlot(slot);
			timetable.Publish();
			await Timetables.InsertAsync(timetable, autoSave: true);

			var result = await Allocations.GetScheduleBreakdownAsync(groupedAssignments[0].Id);

			result.AllocatedPeriodsPerWeek.ShouldBe(4);
			result.ScheduledPeriods.ShouldBe(1);
			result.Lectures.Single().Subjects.Select(subject => subject.SubjectName)
				.ShouldBe(new[] { "French", "German" });
		});
	}

	[Fact]
	public async Task Should_Filter_Lectures_By_The_Callers_Division_Or_Teacher_Scope()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await AddTimetableAsync();
			var assignment = await GetRequiredService<IRepository<AssignedSubject, Guid>>()
				.GetAsync(AssignmentId);
			var queryService = GetRequiredService<SubjectScheduleBreakdownQueryService>();

			var hidden = await queryService.GetAsync(
				assignment,
				Array.Empty<Guid>(),
				[Guid.NewGuid()]);
			var visibleByTeacher = await queryService.GetAsync(
				assignment,
				Array.Empty<Guid>(),
				[OldTeacher]);

			hidden.ScheduledPeriods.ShouldBe(0);
			visibleByTeacher.ScheduledPeriods.ShouldBe(1);
		});
	}
}
