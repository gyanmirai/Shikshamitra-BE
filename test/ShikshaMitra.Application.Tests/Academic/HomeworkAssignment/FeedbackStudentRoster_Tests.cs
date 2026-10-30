using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class FeedbackStudentRoster_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Feedback_Should_Include_Active_Students_Admitted_After_The_Task(bool assignment)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var taskId = Guid.NewGuid();
				if (assignment)
				{
					var type = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "Roster assignment",
						defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
					var task = new AssignmentEntity(taskId, StandardId, SubjectId, "Roster assignment", "Read chapter", type.Id,
						SubmissionType.OnlineText, 10, TenantId, YearId, educationBoardId: medium.EducationBoardId,
						educationMediumId: AcademicMediumId, evaluationRequired: true, evaluationMethod: EvaluationMethod.Marks);
					task.DivisionSchedules.Add(new AssignmentDivisionSchedule(Guid.NewGuid(), taskId, DivisionId,
						null, DateTime.UtcNow.AddDays(1), TenantId));
					await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(task, autoSave: true);
				}
				else
				{
					var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Roster homework");
					var task = new HomeworkAssignmentEntity(taskId, StandardId, SubjectId, "Roster homework", "Read chapter", type.Id,
						SubmissionType.OnlineText, TenantId, YearId, educationBoardId: medium.EducationBoardId,
						educationMediumId: AcademicMediumId);
					task.DivisionSchedules.Add(new HomeworkDivisionSchedule(Guid.NewGuid(), taskId, DivisionId,
						null, DateTime.UtcNow.AddDays(1), TenantId));
					await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(task, autoSave: true);
				}

				var students = GetRequiredService<IRepository<Student, Guid>>();
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var standardStageId = standard.EducationMediumStageId;
				var otherClass = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
					new Standard(Guid.NewGuid(), TenantId, "Other class", 10, standard.EducationMediumStageId, null), autoSave: true);
				var otherDivision = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					new Division(Guid.NewGuid(), TenantId, "Other division", StandardId, 35, "OTHER"), autoSave: true);
				var otherMedium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
					new EducationMedium(Guid.NewGuid(), TenantId, "Other medium", medium.EducationBoardId), autoSave: true);
				Student Person(string name, int roll, StudentLifeCycleStatusEnum status = StudentLifeCycleStatusEnum.Enrolled,
					Guid? division = null, Guid? tenant = null, Guid? standard = null, Guid? academicMedium = null) =>
					new(Guid.NewGuid(), tenant ?? TenantId, name, null, "Student", board.AcademicYearId, medium.EducationBoardId,
						academicMedium ?? AcademicMediumId, standardStageId, standard ?? StandardId, division ?? DivisionId,
						grNo: 1000 + roll, rollNo: roll, status: status);
				var seeded = Person("Existing", 1, StudentLifeCycleStatusEnum.Admission);
				seeded.ApproveAdmission();
				await students.InsertAsync(seeded, autoSave: true);
				async Task<HomeworkSubmissionsResponseDto> ReadAsync()
				{
					if (!assignment) return await GetRequiredService<IHomeworkFeedbackAppService>().GetSubmissionsAsync(AcademicMediumId,
						new GetHomeworkSubmissionsFilterDto { HomeworkId = taskId, ClassId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId });
					var response = await GetRequiredService<IAssignmentFeedbackAppService>().GetSubmissionsAsync(AcademicMediumId,
						new GetAssignmentSubmissionsFilterDto { AssignmentId = taskId, ClassId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId });
					return new HomeworkSubmissionsResponseDto { Data = response.Data, Summary = response.Summary };
				}
				(await ReadAsync()).Data.Select(row => row.StudentId).ShouldBe(new[] { seeded.Id });
				var admitted = Person("Newly activated", 4);
				admitted.AssignGrNumber("GR-000013", null, "Onboarding identifier allocation");
				admitted.AdmissionApprovalStatus.ShouldBe(StudentEnrollmentApprovalState.Pending);
				await students.InsertAsync(admitted, autoSave: true);
				foreach (var excluded in new[]
				{
					Person("Inactive", 5, StudentLifeCycleStatusEnum.InActive),
					Person("Left", 6, StudentLifeCycleStatusEnum.Left),
					Person("Pending admission", 7, StudentLifeCycleStatusEnum.Admission),
					Person("Other division", 8, division: otherDivision.Id),
					Person("Other class", 9, standard: otherClass.Id),
					Person("Other medium", 10, academicMedium: otherMedium.Id),
					Person("Other tenant", 11, tenant: Guid.NewGuid()),
					Person("Deleted", 12)
				})
				{
					await students.InsertAsync(excluded, autoSave: true);
					if (excluded.FirstName == "Deleted") await students.DeleteAsync(excluded, autoSave: true);
				}
				var result = await ReadAsync();
				result.Data.Select(row => row.StudentId).ShouldBe(new[] { seeded.Id, admitted.Id });
				result.Data[1].GrNumber.ShouldBe("GR-000013");
				result.Data[1].Status.ShouldBe((int)SubmissionStatus.NotSubmitted);
				result.Data[1].EvaluationStatus.ShouldBe("Not Started");
				result.Summary.TotalStudents.ShouldBe(2);
				result.Summary.NotSubmitted.ShouldBe(2);
			}
		});
	}
}
