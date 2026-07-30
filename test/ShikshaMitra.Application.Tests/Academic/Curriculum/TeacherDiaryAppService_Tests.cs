using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherDiaryAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid CurrentTeacherId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

	private readonly ILessonPlanAppService _lessonPlanAppService;
	private readonly IRepository<LessonPlan, Guid> _lessonPlanRepository;
	private readonly IRepository<TeacherDiary, Guid> _teacherDiaryRepository;
	private readonly ICurrentTenant _currentTenant;

	public TeacherDiaryAppService_Tests()
	{
		_lessonPlanAppService = GetRequiredService<ILessonPlanAppService>();
		_lessonPlanRepository = GetRequiredService<IRepository<LessonPlan, Guid>>();
		_teacherDiaryRepository = GetRequiredService<IRepository<TeacherDiary, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task UpdateTeacherDiaryAsync_Should_Save_Changes_When_Diary_Is_Submitted()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var plan = await CreateApprovedLessonPlanAsync();
				var diary = await CreateSubmittedDiaryAsync(plan);

				var result = await _lessonPlanAppService.UpdateTeacherDiaryAsync(
					diary.Id,
					new CreateUpdateTeacherDiaryDto
					{
						LessonPlanId = plan.Id,
						ActualDate = plan.PlannedDate,
						ClassConducted = true,
						ActualTopicCovered = "Updated submitted diary topic",
						ActualContentSummary = "Updated notes after submission.",
						ActualCompletionPercentage = 90,
						TeachingMethodUsed = "Activity based",
						StudentResponse = "Students responded well.",
						ClassUnderstandingLevel = "High",
						StudentEngagementLevel = "High",
						Reflection = "Submission can still be refined.",
						WhatWentWell = "Evidence and notes were improved.",
						TeacherRemarks = "Updated after submitted state."
					});

				result.Status.ShouldBe(TeacherDiaryStatus.Submitted);
				result.ActualTopicCovered.ShouldBe("Updated submitted diary topic");
				result.ActualCompletionPercentage.ShouldBe(90);

				var saved = await _teacherDiaryRepository.GetAsync(diary.Id);
				saved.Status.ShouldBe(TeacherDiaryStatus.Submitted);
				saved.ActualTopicCovered.ShouldBe("Updated submitted diary topic");
				saved.ActualCompletionPercentage.ShouldBe(90);
			}
		});
	}

	private async Task<LessonPlan> CreateApprovedLessonPlanAsync()
	{
		var plan = new LessonPlan(
			Guid.NewGuid(),
			TestTenantId,
			academicYearId: Guid.NewGuid(),
			teacherId: CurrentTeacherId,
			standardId: Guid.NewGuid(),
			divisionId: Guid.NewGuid(),
			subjectId: Guid.NewGuid(),
			chapterId: Guid.NewGuid(),
			plannedDate: DateTime.Today,
			topic: "Body parts learning",
			plannedCompletionPercentage: 100);
		plan.Submit();
		plan.Approve(Guid.NewGuid(), "Approved for diary entry.");

		return await _lessonPlanRepository.InsertAsync(plan, autoSave: true);
	}

	private async Task<TeacherDiary> CreateSubmittedDiaryAsync(LessonPlan plan)
	{
		var diary = new TeacherDiary(
			Guid.NewGuid(),
			TestTenantId,
			plan.Id,
			plan.TeacherId,
			plan.PlannedDate);
		diary.Update(
			plan.PlannedDate,
			classConducted: true,
			actualTopicCovered: "Initial submitted diary topic",
			actualContentSummary: "Initial notes.",
			actualCompletionPercentage: 75,
			teachingMethodUsed: "Explanation",
			studentResponse: "Good",
			classUnderstandingLevel: "Medium",
			studentEngagementLevel: "Medium",
			needRevision: false,
			reflection: null,
			whatWentWell: null,
			whatWasDifficult: null,
			studentsNeedingSupport: null,
			nextClassFollowUp: null,
			remedialActionRequired: null,
			homeworkAssigned: null,
			missedReason: null,
			rescheduledToDate: null,
			isBackdated: false,
			backdatedReason: null,
			teacherRemarks: null);
		diary.Submit();

		return await _teacherDiaryRepository.InsertAsync(diary, autoSave: true);
	}
}
