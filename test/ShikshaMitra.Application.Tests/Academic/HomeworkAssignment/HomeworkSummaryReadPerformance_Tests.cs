using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkSummaryReadPerformance_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Homework_List_Should_Aggregate_All_Submission_Statuses_For_Only_The_Requested_Page()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Summary practice");
				var homeworks = GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
				var submissions = GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
				SubmissionStatus[] statuses = [SubmissionStatus.Submitted, SubmissionStatus.Late, SubmissionStatus.Reviewed,
					SubmissionStatus.NeedsReview, SubmissionStatus.Draft, SubmissionStatus.NotSubmitted];
				Guid lastTaskId = default;
				for (var index = 0; index < 12; index++)
				{
					var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, $"Summary {index:00}", "Details",
						type.Id, SubmissionType.OnlineText, TenantId, educationMediumId: AcademicMediumId);
					homework.AddDivisionSchedule(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, DivisionId, null, DateTime.UtcNow.Date, TenantId));
					typeof(HomeworkAssignmentEntity).GetProperty(nameof(homework.CreationTime))!.SetValue(homework, DateTime.UtcNow.Date.AddMinutes(index));
					await homeworks.InsertAsync(homework, autoSave: true);
					lastTaskId = homework.Id;
					await submissions.InsertManyAsync(Enumerable.Range(0, 30).Select(number => new StudentSubmissionEntity(Guid.NewGuid(),
						homework.Id, Guid.NewGuid(), DivisionId, DateTime.UtcNow.Date, TenantId)
					{
						Status = statuses[number % statuses.Length], SubmittedContent = new string('b', 12000)
					}), autoSave: true);
				}
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var submissionTable = db.Model.FindEntityType(typeof(StudentSubmissionEntity))!.GetTableName()!;
				var service = GetRequiredService<IHomeworkAppService>();
				foreach (var page in new[] { 1, 2 })
				{
					db.ChangeTracker.Clear();
					using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
					var result = await service.GetListAsync(AcademicMediumId, new()
					{
						StandardId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId, Page = page, PageSize = 10
					});
					result.TotalCount.ShouldBe(12);
					result.Items.Count.ShouldBe(page == 1 ? 10 : 2);
					result.Items.ShouldAllBe(row => row.SubmittedCount == 15 && row.LateCount == 5 && row.CheckedCount == 5 &&
						row.EvaluationPendingCount == 15 && row.TotalStudents == 0 && row.PendingCount == 0);
					var reads = capture.Reads.Where(read => read.Sql.Contains(submissionTable, StringComparison.Ordinal)).ToList();
					reads.ShouldNotBeEmpty();
					reads.ShouldAllBe(read => read.Rows <= result.Items.Count + 1);
					capture.Commands.ShouldAllBe(sql => !sql.Contains("\"SubmittedContent\"", StringComparison.Ordinal));
					db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
				}
				db.ChangeTracker.Clear();
				using var otherReads = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
				var detail = await service.GetAsync(lastTaskId, AcademicMediumId);
				detail.Data.SubmittedCount.ShouldBe(15);
				detail.Data.CheckedCount.ShouldBe(5);
				var dashboard = await service.GetDashboardAsync(AcademicMediumId,
					new() { StandardId = StandardId, DivisionId = DivisionId, SubjectId = SubjectId });
				dashboard.TotalTasks.ShouldBe(12);
				dashboard.EvaluationPendingCount.ShouldBe(12);
				dashboard.EvaluationPending.Count.ShouldBe(12);
				dashboard.EvaluationPending.ShouldAllBe(row => row.SubmittedCount == 15 && row.LateCount == 5 && row.EvaluationPendingCount == 15);
				var summaryReads = otherReads.Reads.Where(read => read.Sql.Contains(submissionTable, StringComparison.Ordinal)).ToList();
				summaryReads.ShouldNotBeEmpty();
				summaryReads.ShouldAllBe(read => read.Rows <= 13);
				otherReads.Commands.ShouldAllBe(sql => !sql.Contains("\"SubmittedContent\"", StringComparison.Ordinal));
				db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
			}
		});
	}
}
