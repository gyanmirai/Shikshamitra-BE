using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
	[Fact]
	public async Task BulkSaveMarksAsync_Should_Update_Insert_And_Preserve_Attendance_On_Retry()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, students) = await CreateBulkMarksFixtureAsync();
				var subjectId = exam.ExamSubjects.First().SubjectId;
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var marks = GetRequiredService<IRepository<ExamMark, Guid>>();
				var existing = new ExamMark(Guid.NewGuid(), exam.Id, students[0].Id, subjectId,
					divisionId, 20, false, tenantId: TestTenantId);
				await marks.InsertAsync(existing, autoSave: true);
				var input = new BulkSaveMarksInputDto
				{
					SubjectId = subjectId,
					DivisionId = divisionId,
					Rows =
					[
						new() { StudentId = students[0].Id, AttendanceStatus = 0, ObtainedMarks = 82, Feedback = "Improved" },
						new() { StudentId = students[1].Id, AttendanceStatus = 2, ObtainedMarks = 0, Feedback = "Discarded" }
					]
				};

				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var markWriteBatches = new List<int>();
				void CountSaves(object? sender, SavingChangesEventArgs args)
				{
					var changedMarks = db.ChangeTracker.Entries<ExamMark>().Count(entry =>
						entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
					// ABP may flush the ambient unit of work again without pending mark writes.
					if (changedMarks > 0) markWriteBatches.Add(changedMarks);
				}
				db.SavingChanges += CountSaves;
				try
				{
					await _examAppService.BulkSaveMarksAsync(exam.Id, input);
					markWriteBatches.ShouldBe(new[] { 2 });
					markWriteBatches.Clear();
					await _examAppService.BulkSaveMarksAsync(exam.Id, input);
					markWriteBatches.ShouldBe(new[] { 2 });
				}
				finally
				{
					db.SavingChanges -= CountSaves;
				}

				var saved = await marks.GetListAsync(mark => mark.ExamId == exam.Id && mark.SubjectId == subjectId);
				saved.Count.ShouldBe(2);
				var present = saved.Single(mark => mark.StudentId == students[0].Id);
				present.Id.ShouldBe(existing.Id);
				present.MarksObtained.ShouldBe(82);
				present.Feedback.ShouldBe("Improved");
				var absent = saved.Single(mark => mark.StudentId == students[1].Id);
				absent.AttendanceStatus.ShouldBe(ExamAttendanceStatus.MedicalLeave);
				absent.IsAbsent.ShouldBeTrue();
				absent.MarksObtained.ShouldBeNull();
				absent.Feedback.ShouldBeNull();
			}
		});
	}

	[Fact]
	public async Task BulkSaveMarksAsync_Should_Validate_Later_Rows_Before_Changing_Earlier_Marks()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, students) = await CreateBulkMarksFixtureAsync();
				var subjectId = exam.ExamSubjects.First().SubjectId;
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var marks = GetRequiredService<IRepository<ExamMark, Guid>>();
				var existing = new ExamMark(Guid.NewGuid(), exam.Id, students[0].Id, subjectId,
					divisionId, 20, false, tenantId: TestTenantId);
				await marks.InsertAsync(existing, autoSave: true);

				var exception = await Should.ThrowAsync<UserFriendlyException>(() => _examAppService.BulkSaveMarksAsync(exam.Id,
					new BulkSaveMarksInputDto
					{
						SubjectId = subjectId,
						DivisionId = divisionId,
						Rows =
						[
							new() { StudentId = students[0].Id, AttendanceStatus = 0, ObtainedMarks = 82 },
							new() { StudentId = students[1].Id, AttendanceStatus = 0, ObtainedMarks = 101 }
						]
					}));

				exception.Message.ShouldBe("Obtained marks must be between 0 and 100");
				existing.MarksObtained.ShouldBe(20);
				(await marks.CountAsync(mark => mark.ExamId == exam.Id)).ShouldBe(1);
			}
		});
	}

	[Fact]
	public async Task BulkSaveMarksAsync_Should_Reject_A_Student_Assigned_To_Another_Elective()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, students) = await CreateBulkMarksFixtureAsync();
				var subjects = exam.ExamSubjects.ToList();
				var standard = await _standardRepository.GetAsync(exam.StandardId);
				var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
				var group = new ElectiveGroup(Guid.NewGuid(), TestTenantId, "Bulk elective", "#123456", stage.EducationMediumId);
				group.AddSubject(subjects[0].SubjectId);
				group.AddSubject(subjects[1].SubjectId);
				await _electiveGroupRepository.InsertAsync(group, autoSave: true);
				await _assignedSubjectRepository.InsertAsync(new AssignedSubject(Guid.NewGuid(), TestTenantId,
					exam.StandardId, subjects[0].SubjectId, "BULK", SubjectStatus.Published, SubjectType.Elective, group.Id), autoSave: true);
				for (var index = 0; index < students.Length; index++)
				{
					var selection = new StudentElectiveSelection(Guid.NewGuid(), TestTenantId, students[index].Id,
						group.Id, subjects[index].SubjectId, stage.EducationMediumId);
					selection.LockSelection();
					await _selectionRepository.InsertAsync(selection, autoSave: true);
				}

				var exception = await Should.ThrowAsync<UserFriendlyException>(() => _examAppService.BulkSaveMarksAsync(exam.Id,
					new BulkSaveMarksInputDto
					{
						SubjectId = subjects[0].SubjectId,
						DivisionId = exam.ExamDivisions.Single().DivisionId,
						Rows = [new() { StudentId = students[1].Id, AttendanceStatus = 0, ObtainedMarks = 50 }]
					}));
				exception.Message.ShouldBe("This student is not assigned to the selected elective subject.");
				(await GetRequiredService<IRepository<ExamMark, Guid>>().CountAsync(mark => mark.ExamId == exam.Id)).ShouldBe(0);
				var eligible = await _examAppService.GetMarksAsync(new GetExamMarksInput
				{
					ExamId = exam.Id, SubjectId = subjects[0].SubjectId,
					DivisionId = exam.ExamDivisions.Single().DivisionId
				});
				eligible.TotalCount.ShouldBe(1);
				eligible.Items.Single().StudentId.ShouldBe(students[0].Id);
			}
		});
	}

	[Fact]
	public async Task GetMarksAsync_Should_Page_Display_Rows_And_Keep_Publication_Global_To_The_Division()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, students) = await CreateBulkMarksFixtureAsync(studentCount: 12);
				var subjectId = exam.ExamSubjects.First().SubjectId;
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var marks = students.Select((student, index) =>
				{
					var mark = new ExamMark(Guid.NewGuid(), exam.Id, student.Id, subjectId, divisionId, 70 + index,
						false, tenantId: TestTenantId);
					if (index < 11) mark.Publish();
					return mark;
				}).ToList();
				await GetRequiredService<IRepository<ExamMark, Guid>>().InsertManyAsync(marks, autoSave: true);
				var input = new GetExamMarksInput
				{
					ExamId = exam.Id, SubjectId = subjectId, DivisionId = divisionId, Page = 1, PageSize = 10
				};
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var firstPage = await _examAppService.GetMarksAsync(input);
				firstPage.TotalCount.ShouldBe(12);
				firstPage.TotalPages.ShouldBe(2);
				firstPage.Items.Select(item => item.StudentId).ShouldBe(students.Take(10).Select(student => student.Id));
				firstPage.IsMarksPublish.ShouldBeFalse();
				input.Page = 2;
				var secondPage = await _examAppService.GetMarksAsync(input);
				secondPage.Items.Select(item => item.StudentId).ShouldBe(students.Skip(10).Select(student => student.Id));
				input.Page = 1;
				input.Search = "Bulk12";
				var searched = await _examAppService.GetMarksAsync(input);
				searched.TotalCount.ShouldBe(1);
				searched.Items.Single().ObtainedMarks.ShouldBe(81);
				searched.IsMarksPublish.ShouldBeFalse();
				db.ChangeTracker.Entries().ShouldNotContain(entry =>
					entry.Entity.GetType().Namespace != null &&
					entry.Entity.GetType().Namespace!.StartsWith("ShikshaMitra.TenantSettings"));
			}
		});
	}

	private async Task<(Exam Exam, Student[] Students)> CreateBulkMarksFixtureAsync(int studentCount = 2)
	{
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: FakeAdminUserId, description: "Tenant-wide bulk marks test scope"), autoSave: true);
		await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);
		var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Published);
		var standard = await _standardRepository.GetAsync(exam.StandardId);
		var medium = await _mediumRepository.GetAsync(mediumId);
		var board = await _boardRepository.GetAsync(medium.EducationBoardId);
		var students = Enumerable.Range(1, studentCount).Select(index => CreateApprovedStudent($"Bulk{index}", index,
			board.AcademicYearId, board.Id, mediumId, standard.EducationMediumStageId, standard.Id,
			exam.ExamDivisions.Single().DivisionId, Guid.NewGuid().ToString("N"))).ToArray();
		await _studentRepository.InsertManyAsync(students, autoSave: true);
		return (exam, students);
	}
}
