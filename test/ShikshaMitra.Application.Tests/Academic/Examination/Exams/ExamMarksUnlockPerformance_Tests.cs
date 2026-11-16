using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
	[Fact]
	public async Task UnlockMarks_Should_Flush_All_Marks_And_Workflow_Together()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, students) = await CreateBulkMarksFixtureAsync(12);
				var subjectId = exam.ExamSubjects.First().SubjectId;
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var marks = students.Select(student =>
				{
					var mark = new ExamMark(Guid.NewGuid(), exam.Id, student.Id, subjectId, divisionId,
						75, false, tenantId: TestTenantId);
					mark.Publish();
					return mark;
				}).ToList();
				var markRepository = GetRequiredService<IRepository<ExamMark, Guid>>();
				await markRepository.InsertManyAsync(marks, autoSave: true);
				var workflowRepository = GetRequiredService<IRepository<ExamMarksWorkflowState, Guid>>();
				var workflow = new ExamMarksWorkflowState(Guid.NewGuid(), exam.Id, subjectId, divisionId, TestTenantId);
				workflow.TransitionTo(ExamMarksWorkflowStatus.Published);
				await workflowRepository.InsertAsync(workflow, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var dirtySaves = 0;
				EventHandler<SavingChangesEventArgs> onSave = (_, _) =>
				{
					if (db.ChangeTracker.Entries().Any(entry =>
						entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) dirtySaves++;
				};
				db.SavingChanges += onSave;
				try
				{
					await _examAppService.UnlockMarksAsync(exam.Id, new UnlockWorkflowInputDto
					{
						SubjectId = subjectId, DivisionId = divisionId, Reason = " Correct an audited entry "
					});
				}
				finally { db.SavingChanges -= onSave; }

				dirtySaves.ShouldBe(1, "Unlocking a division must not flush once per student's mark.");
				db.ChangeTracker.Clear();
				var savedMarks = await markRepository.GetListAsync(mark => mark.ExamId == exam.Id &&
					mark.SubjectId == subjectId && mark.DivisionId == divisionId);
				savedMarks.Count.ShouldBe(students.Length);
				savedMarks.ShouldAllBe(mark => !mark.IsPublished && mark.MarksObtained == 75);
				var savedWorkflow = await workflowRepository.GetAsync(workflow.Id);
				savedWorkflow.Status.ShouldBe(ExamMarksWorkflowStatus.Draft);
				savedWorkflow.UnlockReason.ShouldBe("Correct an audited entry");
			}
		});
	}
}
