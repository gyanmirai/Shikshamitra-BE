using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicDashboardTeacherAggregation_Tests : LessonPlanProgressTestBase
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Teacher_Summaries_Should_Preserve_Assignment_And_Submission_Status_Behavior(bool hasAssignment)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();
				if (hasAssignment)
				{
					var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
					var assigned = await repository.GetAsync(item => item.SubjectId == SubjectId);
					assigned.SetPrimaryTeacher(TeacherEmployeeId);
					await repository.UpdateAsync(assigned, autoSave: true);
				}

				for (var index = 0; index < 3; index++)
				{
					var plan = await InsertPlanAsync();
					plan.Submit();
					await Plans.UpdateAsync(plan, autoSave: true);
				}
				var unrelatedInput = Input();
				unrelatedInput.TeacherId = Guid.NewGuid();
				var unrelatedPlan = await InsertPlanAsync(unrelatedInput);
				unrelatedPlan.Submit();
				await Plans.UpdateAsync(unrelatedPlan, autoSave: true);

				var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(
					AcademicMediumId, "Dashboard practice", defaultSubmissionType: SubmissionType.OnlineText,
					defaultMarks: 10, evaluationRequired: true, defaultEvaluationMethod: EvaluationMethod.Marks);
				var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId,
					"Dashboard homework", "Status aggregation", homeworkType.Id, SubmissionType.OnlineText,
					tenantId: TenantId, educationMediumId: AcademicMediumId);
				await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
				foreach (var status in new[] { SubmissionStatus.Reviewed, SubmissionStatus.Reviewed,
					SubmissionStatus.Submitted, SubmissionStatus.Late, SubmissionStatus.NeedsReview })
				{
					await InsertSubmissionAsync(homework.Id, DivisionId, status);
				}
				await InsertSubmissionAsync(homework.Id, Guid.NewGuid(), SubmissionStatus.Reviewed);

				var service = GetRequiredService<IAcademicDashboardAppService>();
				var assignments = await service.GetTeacherAssignmentsAsync();
				var assessment = await service.GetTeacherAssessmentAsync();
				var attention = await service.GetTeacherAttentionAsync();
				var overview = await service.GetTeacherOverviewAsync();
				var analytics = await service.GetTeacherAnalyticsAsync();
				var dashboard = await service.GetTeacherDashboardAsync();
				overview.AssignedClassesCount.ShouldBe(dashboard.AssignedClassesCount);
				assignments.AssignedClassesCount.ShouldBe(overview.AssignedClassesCount);
				assignments.PendingHomework.ShouldBe(overview.PendingHomework);
				assessment.PendingEvaluations.ShouldBe(overview.PendingEvaluations);
				attention.StudentsAttentionCount.ShouldBe(overview.StudentsAttentionCount);
				overview.PendingEvaluations.ShouldBe(dashboard.PendingEvaluations);
				overview.ChapterProgressChart.StandardNames.ShouldBeEmpty();
				analytics.ChapterProgressChart.StandardNames.Count.ShouldBe(hasAssignment ? 1 : 0);
				analytics.ChapterProgressChart.StandardNames.ShouldBe(dashboard.ChapterProgressChart.StandardNames);
				dashboard.AssignedClassesCount.ShouldBe(hasAssignment ? 1 : 0);
				dashboard.AssignedSubjectsCount.ShouldBe(hasAssignment ? 1 : 0);
				dashboard.PendingHomework.ShouldBe(hasAssignment ? 1 : 0);
				dashboard.PendingEvaluations.ShouldBe(hasAssignment ? 3 : 0);
				dashboard.HomeworkStatus.Completed.ShouldBe(hasAssignment ? 2 : 0);
				dashboard.HomeworkStatus.Graded.ShouldBe(hasAssignment ? 2 : 0);
				dashboard.HomeworkStatus.Pending.ShouldBe(hasAssignment ? 2 : 0);
				dashboard.EvaluationsRate.ShouldBe(hasAssignment ? 40 : 0);
				dashboard.ChapterProgressChart.StandardNames.Count.ShouldBe(hasAssignment ? 1 : 0);
				dashboard.ActionCenterItems.Single(item => item.Id == "lesson-review").Count.ShouldBe(3);
				dashboard.ReadinessItems.Single(item => item.Key == "lesson-diary-readiness").TotalCount.ShouldBe(3);
			}
		});
	}

	private Task<StudentSubmissionEntity> InsertSubmissionAsync(Guid homeworkId, Guid divisionId, SubmissionStatus status) =>
		GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(
			new StudentSubmissionEntity(Guid.NewGuid(), homeworkId, Guid.NewGuid(), divisionId,
				DateTime.UtcNow.AddDays(1), TenantId) { Status = status }, autoSave: true);
}
