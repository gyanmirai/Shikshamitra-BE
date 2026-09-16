using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
	[Fact]
	public async Task GetListAsync_Should_Hydrate_Only_The_Page_And_Keep_All_Status_Counts()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (template, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
				var createdAt = DateTime.UtcNow.AddHours(-1);
				typeof(Exam).GetProperty(nameof(Exam.CreationTime))!.SetValue(template, createdAt);
				await _examRepository.UpdateAsync(template, autoSave: true);
				var exams = new List<Exam> { template };
				for (var index = 1; index < 12; index++)
				{
					var exam = new Exam(Guid.NewGuid(), template.ExamTypeId, $"Paged exam {index}", template.StandardId, TestTenantId);
					exam.ChangeStatus(index % 3 == 0 ? ExamStatus.Pending : index % 3 == 1 ? ExamStatus.Published : ExamStatus.Completed);
					typeof(Exam).GetProperty(nameof(Exam.CreationTime))!.SetValue(exam, createdAt.AddMinutes(index));
					foreach (var division in template.ExamDivisions) exam.AddDivision(Guid.NewGuid(), division.DivisionId);
					foreach (var schedule in template.ExamSubjects)
					{
						var subject = new ExamSubject(Guid.NewGuid(), exam.Id, schedule.SubjectId);
						subject.UpdateSchedule(schedule.ExamDate, schedule.StartTime, schedule.EndTime,
							schedule.TotalMarks, schedule.PassingMarks, new string('n', 900));
						exam.AddSubject(subject);
					}
					exams.Add(exam);
				}
				await _examRepository.InsertManyAsync(exams.Skip(1), autoSave: true);
				var expectedIds = exams.Where(exam => exam.Status == ExamStatus.Published).Skip(1).Take(2).Select(exam => exam.Id).ToList();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();

				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId, Status = ExamStatus.Published, SkipCount = 1, MaxResultCount = 2,
					StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(7)
				});

				result.TotalCount.ShouldBe(4);
				result.PendingCount.ShouldBe(4);
				result.PublishedCount.ShouldBe(4);
				result.CompletedCount.ShouldBe(4);
				result.OngoingCount.ShouldBe(0);
				result.Items.Select(item => item.ExamId).ShouldBe(expectedIds);
				result.Items.ShouldAllBe(item => item.SubjectCount == 2 && item.MissingRoomAssignmentCount == 2);
				result.Items.ShouldAllBe(item => item.StartDate == DateTime.Today.AddDays(1) && item.EndDate == DateTime.Today.AddDays(6).AddHours(15));
				db.ChangeTracker.Entries<Exam>().Select(entry => entry.Entity.Id).ShouldBe(expectedIds, ignoreOrder: true);
				db.ChangeTracker.Entries<ExamSubject>().Count().ShouldBe(4);
				db.ChangeTracker.Entries().ShouldNotContain(entry =>
					entry.Entity.GetType().Namespace != null &&
					entry.Entity.GetType().Namespace!.StartsWith("ShikshaMitra.TenantSettings"));
			}
		});
	}
}
