using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Fact]
	public async Task GetSeatAllocationTableAsync_Should_Filter_Before_Paging_And_Load_Only_Page_Details()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (template, _, room, students) = await CreateAllocatedSessionPlanAsync();
				var assignedIds = new List<Guid>();
				for (var index = 0; index < 6; index++)
				{
					var exam = new Exam(Guid.Parse($"77000000-0000-0000-0000-{index + 1:000000000000}"),
						template.ExamTypeId, $"PAGE projection {index}", template.StandardId, TestTenantId);
					exam.ChangeStatus(ExamStatus.Published);
					foreach (var division in template.ExamDivisions) exam.AddDivision(Guid.NewGuid(), division.DivisionId);
					foreach (var subject in template.ExamSubjects)
						exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.SubjectId,
							subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks, subject.PassingMarks));
					await _examRepository.InsertAsync(exam, autoSave: true);
					if (index % 2 != 0) continue;
					assignedIds.Add(exam.Id);
					var plan = new ExamSeatingPlan(Guid.NewGuid(), TestTenantId, exam.Id, "Projected plan", SeatingPlanScopeType.AllSubjects);
					await _seatingPlanRepository.InsertAsync(plan, autoSave: true);
					await _seatAllocationRepository.InsertAsync(new ExamSeatAllocation(Guid.NewGuid(), TestTenantId,
						plan.Id, exam.Id, students[0].Id, room.Id, 1, null, isRoomAssign: true), autoSave: true);
				}
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();

				var result = await _seatAllocationAppService.GetSeatAllocationTableAsync(new GetSeatAllocationTableInput
				{
					StandardId = template.StandardId, Search = "page PROJECTION", IsRoomAssigned = true, Page = 2, PageSize = 1
				});

				result.TotalCount.ShouldBe(3);
				result.TotalPages.ShouldBe(3);
				result.CurrentPage.ShouldBe(2);
				var row = result.Items.Single();
				row.ExamId.ShouldBe(assignedIds[1]);
				row.TotalStudents.ShouldBe(students.Count);
				row.AllocatedStudents.ShouldBe(1);
				row.DivisionProgress.Sum(division => division.AllocatedStudents).ShouldBe(1);
				row.SubjectCount.ShouldBe(template.ExamSubjects.Count);
				db.ChangeTracker.Entries<Exam>().Select(entry => entry.Entity.Id).ShouldBe([assignedIds[1]]);
				db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
				db.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
			}
		});
	}
}
