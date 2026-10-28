using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class LearningTaskReadPerformance_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Feedback_Roster_Should_Project_Approved_Students_Without_Hydrating_Their_Graphs(bool assignment)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var studentRepository = GetRequiredService<IRepository<Student, Guid>>();
				Guid reviewedStudent = default;
				foreach (var roll in new int?[] { 2, 1, null, 9 })
				{
					var student = new Student(Guid.NewGuid(), TenantId, $"Student {roll}", null, "Surname",
						board.AcademicYearId, medium.EducationBoardId, medium.Id, standard.EducationMediumStageId,
						StandardId, DivisionId, rollNo: roll, status: StudentLifeCycleStatusEnum.Admission);
					if (roll != 9) student.ApproveAdmission();
					await studentRepository.InsertAsync(student, autoSave: true);
					if (roll == 2) reviewedStudent = student.Id;
				}
				Guid taskId;
				if (assignment)
				{
					var type = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "Roster assignment", defaultMarks: 10,
						defaultEvaluationMethod: EvaluationMethod.Marks);
					var task = new AssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Roster assignment", "Test", type.Id,
						SubmissionType.OnlineText, 10, TenantId, board.AcademicYearId, educationBoardId: medium.EducationBoardId,
						educationMediumId: medium.Id, educationMediumStageId: standard.EducationMediumStageId,
						evaluationRequired: true, evaluationMethod: EvaluationMethod.Marks);
					task.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), task.Id, DivisionId, null, DateTime.UtcNow.Date, TenantId));
					await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(task, autoSave: true);
					taskId = task.Id;
				}
				else
				{
					var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Roster homework");
					var task = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Roster homework", "Test", type.Id,
						SubmissionType.OnlineText, TenantId, educationMediumId: medium.Id);
					task.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), task.Id, DivisionId, null, DateTime.UtcNow.Date, TenantId));
					await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(task, autoSave: true);
					taskId = task.Id;
				}
				var submission = assignment
					? new StudentSubmissionEntity(Guid.NewGuid(), taskId, reviewedStudent, DivisionId, DateTime.UtcNow.Date, TenantId, true)
					: new StudentSubmissionEntity(Guid.NewGuid(), taskId, reviewedStudent, DivisionId, DateTime.UtcNow.Date, TenantId);
				submission.Status = SubmissionStatus.Reviewed;
				submission.Marks = 8;
				submission.TotalMarks = 10;
				await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(submission, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
				var rows = assignment
					? (await GetRequiredService<IAssignmentFeedbackAppService>().GetSubmissionsAsync(AcademicMediumId,
						new() { AssignmentId = taskId, ClassId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId })).Data
					: (await GetRequiredService<IHomeworkFeedbackAppService>().GetSubmissionsAsync(AcademicMediumId,
						new() { HomeworkId = taskId, ClassId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId })).Data;
				rows.Count.ShouldBe(3);
				rows.Select(row => row.RollNumber).ShouldBe(new[] { "1", "2", "" });
				rows.Single(row => row.StudentId == reviewedStudent).Marks.ShouldBe(8);
				rows.Single(row => row.StudentId == reviewedStudent).StudentName.ShouldBe("Student 2  Surname");
				capture.Reads.ShouldNotBeEmpty();
				var studentTable = db.Model.FindEntityType(typeof(Student))!.GetTableName()!;
				var studentReads = capture.Reads.Where(read => read.Sql.Contains(studentTable, StringComparison.Ordinal)).ToList();
				studentReads.ShouldNotBeEmpty();
				studentReads.Select(read => read.Sql[..read.Sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)])
					.ShouldAllBe(select => !select.Contains("\"BankDetailId\"", StringComparison.Ordinal));
				db.ChangeTracker.Entries<Student>().ShouldBeEmpty("Rosters need identity scalars, not student aggregate graphs.");
			}
		});
	}
}
