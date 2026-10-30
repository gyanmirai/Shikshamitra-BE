using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class LearningTaskCalendarReadPerformance_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task Create_Should_Resolve_Calendar_And_Due_Date_Candidates_Once_For_All_Divisions(bool assignment, bool blocked)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(board.AcademicYearId);
				var divisions = new List<Guid> { DivisionId };
				for (var index = 1; index < 10; index++)
				{
					var division = new Division(Guid.NewGuid(), TenantId, $"Calendar {index}", StandardId, 35, string.Empty);
					await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);
					divisions.Add(division.Id);
				}
				var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, AcademicMediumId, year.Id, "Schedule checks",
					year.StartDate, year.EndDate, isDefault: true);
				calendar.Publish(ActorId);
				await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
				var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Calendar practice");
				var assignmentType = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "Calendar assignment",
					defaultSubmissionType: SubmissionType.OnlineText, defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
				var publish = year.StartDate.Date.AddDays(5);
				await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new AcademicCalendarDay(Guid.NewGuid(),
					TenantId, calendar.Id, publish.AddDays(1), DayType.WorkingDay, "School day"), autoSave: true);
				if (blocked)
					await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new AcademicCalendarDay(Guid.NewGuid(),
						TenantId, calendar.Id, publish.AddDays(10), DayType.Holiday, "Last division holiday", appliesToAll: false,
						standardId: StandardId, divisionId: divisions.Last()), autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var calendarTable = db.Model.FindEntityType(typeof(AcademicCalendar))!.GetTableName()!;
				var dayTable = db.Model.FindEntityType(typeof(AcademicCalendarDay))!.GetTableName()!;
				db.ChangeTracker.Clear();
				using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
				if (assignment)
				{
					var input = new CreateAssignmentV2Dto
					{
						AcademicYearId = year.Id, StandardId = StandardId, SubjectId = SubjectId, AssignmentTypeId = assignmentType.Id,
						Title = "Calendar assignment", Description = "Date checks", SubmissionType = (int)SubmissionType.OnlineText,
						DivisionSchedules = divisions.Select((id, index) => new CreateAssignmentV2DivisionScheduleDto
						{ DivisionId = id, PublishAt = publish, DueAt = publish.AddDays(index + 1) }).ToList()
					};
					var service = GetRequiredService<IAssignmentV2AppService>();
					if (blocked)
					{
						var error = await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(AcademicMediumId, input, null));
						error.Code.ShouldBe("ShikshaMitra.Assignment:DueDateBlockedByCalendar");
						error.Data["CalendarDay"].ShouldBe("Last division holiday");
						error.Data["DueDate"].ShouldBe(publish.AddDays(10).ToString("yyyy-MM-dd"));
					}
					else (await service.CreateAsync(AcademicMediumId, input, null)).Data.DivisionSchedules.Count.ShouldBe(10);
					capture.Commands.Count(sql => sql.Contains(calendarTable, StringComparison.Ordinal)).ShouldBe(1);
				}
				else
				{
					var input = new CreateHomeworkDto
					{
						StandardId = StandardId, SubjectId = SubjectId, HomeworkTypeId = homeworkType.Id,
						Title = "Calendar homework", Description = "Date checks", SubmissionType = (int)SubmissionType.OnlineText,
						DivisionSchedules = divisions.Select((id, index) => new CreateHomeworkDivisionScheduleDto
						{ DivisionId = id, PublishAt = publish, DueAt = publish.AddDays(index + 1) }).ToList()
					};
					var service = GetRequiredService<IHomeworkAppService>();
					if (blocked)
					{
						var error = await Should.ThrowAsync<UserFriendlyException>(() => service.CreateAsync(AcademicMediumId, input, null));
						error.Message.ShouldBe($"Due Date {publish.AddDays(10).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} for Calendar 9 is blocked by the academic calendar entry \"Last division holiday\". Choose another due date.");
					}
					else (await service.CreateAsync(AcademicMediumId, input, null)).Data.DivisionSchedules.Count.ShouldBe(10);
				}
				capture.Commands.Count(sql => sql.Contains(dayTable, StringComparison.Ordinal)).ShouldBe(1);
			}
		});
	}
}
