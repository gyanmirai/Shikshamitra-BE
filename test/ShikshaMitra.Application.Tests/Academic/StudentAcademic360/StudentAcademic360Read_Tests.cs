using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.StudentAcademic360;

public class StudentAcademic360Read_Tests : SubjectTeacherReassignmentTestBase
{
	private IStudentAcademic360AppService Academic360 => GetRequiredService<IStudentAcademic360AppService>();

	[Fact]
	public async Task List_Should_Return_One_Scoped_Page_With_Server_Summary_Search_And_Smart_Filters()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var contextYearId = board.AcademicYearId;
				await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
					TenantId, ActorId, academicYearId: contextYearId, educationBoardId: medium.EducationBoardId,
					educationMediumId: AcademicMediumId, educationMediumStageId: standard.EducationMediumStageId,
					standardId: StandardId, divisionId: DivisionId), autoSave: true);
				var repository = GetRequiredService<IRepository<Student, Guid>>();
				var lowAttendance = await repository.InsertAsync(new Student(Guid.NewGuid(), TenantId, "Low", null, "Attendance",
					contextYearId, medium.EducationBoardId, AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId,
					grNo: 1001, rollNo: 1, status: StudentLifeCycleStatusEnum.Enrolled), autoSave: true);
				var fullAttendance = await repository.InsertAsync(new Student(Guid.NewGuid(), TenantId, "Full", null, "Attendance",
					contextYearId, medium.EducationBoardId, AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId,
					grNo: 1002, rollNo: 2, status: StudentLifeCycleStatusEnum.Enrolled), autoSave: true);
				await repository.InsertAsync(new Student(Guid.NewGuid(), TenantId, "No", null, "Data",
					contextYearId, medium.EducationBoardId, AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId,
					grNo: 1003, rollNo: 3, status: StudentLifeCycleStatusEnum.Enrolled), autoSave: true);

				for (var index = 0; index < 4; index++)
				{
					var day = new AttendanceDay(Guid.NewGuid(), TenantId, contextYearId, StandardId, DivisionId,
						DateTime.UtcNow.Date.AddDays(-index), ActorId);
					await GetRequiredService<IRepository<AttendanceDay, Guid>>().InsertAsync(day, autoSave: true);
					await GetRequiredService<IRepository<AttendanceEntry, Guid>>().InsertAsync(new AttendanceEntry(Guid.NewGuid(),
						TenantId, day.Id, lowAttendance.Id, index < 2 ? AttendanceStatus.Present : AttendanceStatus.Absent, ActorId), autoSave: true);
					await GetRequiredService<IRepository<AttendanceEntry, Guid>>().InsertAsync(new AttendanceEntry(Guid.NewGuid(),
						TenantId, day.Id, fullAttendance.Id, AttendanceStatus.Present, ActorId), autoSave: true);
				}

				var input = new GetStudentAcademic360ListInput
				{
					AcademicYearId = contextYearId,
					BoardId = medium.EducationBoardId,
					EducationMediumId = AcademicMediumId,
					SkipCount = 0,
					MaxResultCount = 2
				};
				var page = await Academic360.GetListAsync(input);
				page.TotalCount.ShouldBe(3);
				page.Items.Count.ShouldBe(2);
				page.Summary.TotalStudents.ShouldBe(3);
				page.Summary.AverageAttendance.ShouldBe(75);
				page.Items.First().StudentName.ShouldBe("Low Attendance");

				input.SearchTerm = "1002";
				var searched = await Academic360.GetListAsync(input);
				searched.TotalCount.ShouldBe(1);
				searched.Items.Single().StudentId.ShouldBe(fullAttendance.Id);

				input.SearchTerm = null;
				input.SmartFilter = StudentAcademic360SmartFilter.LowAttendance;
				var filtered = await Academic360.GetListAsync(input);
				filtered.TotalCount.ShouldBe(1);
				filtered.Items.Single().StudentId.ShouldBe(lowAttendance.Id);
			}
		});
	}

	[Fact]
	public async Task Profile_Should_Preserve_Additional_And_Contact_Values_Without_Loading_Organization_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				var birth = new DateTimeOffset(2012, 4, 3, 0, 0, 0, TimeSpan.Zero);
				var inquiry = new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);
				await GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new StudentAdditionalDetail(student.Id,
					TenantId, birth, GenderEnum.Female, inquiry, "ACADEMIC-PROFILE", null, "Previous school")
					{ IsLibraryMember = true, IsTransportMember = true }, autoSave: true);
				await GetRequiredService<IRepository<StudentContactDetail, Guid>>().InsertAsync(new StudentContactDetail(student.Id,
					TenantId, "9000000000", "9000000001", "academic@example.test", "First address", "Second address", "Third address", "Pune",
					pincode: "411001"), autoSave: true);
				var db = await GetDbAsync();
				db.ChangeTracker.Clear();
				var response = await Academic360.GetTabAsync(student.Id, "attendance", new StudentAcademic360Input());
				response.Student.InquiryNo.ShouldBe("ACADEMIC-PROFILE");
				response.Student.DateOfBirth.ShouldBe(birth);
				response.Student.DateOfInquiry.ShouldBe(inquiry);
				response.Student.Gender.ShouldBe(GenderEnum.Female);
				response.Student.PreviousSchool.ShouldBe("Previous school");
				response.Student.IsLibraryMember.ShouldBeTrue();
				response.Student.IsTransportMember.ShouldBeTrue();
				response.Student.PrimaryContactNo.ShouldBe("9000000000");
				response.Student.SecondaryContactNo.ShouldBe("9000000001");
				response.Student.Email.ShouldBe("academic@example.test");
				response.Student.Address1.ShouldBe("First address");
				response.Student.Address2.ShouldBe("Second address");
				response.Student.Address3.ShouldBe("Third address");
				response.Student.City.ShouldBe("Pune");
				response.Student.PinCode.ShouldBe("411001");
				db.ChangeTracker.Entries().ShouldAllBe(entry => entry.Entity.GetType().Namespace == null ||
					!entry.Entity.GetType().Namespace!.StartsWith("ShikshaMitra.TenantSettings", StringComparison.Ordinal));
			}
		});
	}

	[Fact]
	public async Task Attendance_Should_Count_Full_Range_And_Return_Only_Thirty_Recent_Rows()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				var start = new DateTime(2026, 7, 1);
				for (var index = 0; index < 60; index++)
				{
					var day = new AttendanceDay(Guid.NewGuid(), TenantId, YearId, StandardId, DivisionId, start.AddDays(index), ActorId);
					await GetRequiredService<IRepository<AttendanceDay, Guid>>().InsertAsync(day, autoSave: true);
					var status = index < 30 ? AttendanceStatus.Present : index < 50 ? AttendanceStatus.Absent : AttendanceStatus.Pending;
					await GetRequiredService<IRepository<AttendanceEntry, Guid>>().InsertAsync(new AttendanceEntry(Guid.NewGuid(),
						TenantId, day.Id, student.Id, status, ActorId, $"Day {index}"), autoSave: true);
				}
				var db = await GetDbAsync();
				db.ChangeTracker.Clear();
				var response = await Academic360.GetTabAsync(student.Id, "attendance", new StudentAcademic360Input
				{
					AttendanceStartDate = start.AddDays(59), AttendanceEndDate = start
				});
				response.Attendance.TotalDays.ShouldBe(50);
				response.Attendance.PresentDays.ShouldBe(30);
				response.Attendance.AbsentDays.ShouldBe(20);
				response.Attendance.PendingDays.ShouldBe(10);
				response.Attendance.Percentage.ShouldBe(60);
				response.Attendance.RecentHistory.Count.ShouldBe(30);
				response.Attendance.RecentHistory.First().Date.ShouldBe(start.AddDays(59));
				response.Attendance.RecentHistory.Last().Date.ShouldBe(start.AddDays(30));
				db.ChangeTracker.Entries<AttendanceEntry>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AttendanceDay>().ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task Exams_Should_Apply_Visibility_Before_Paging_And_Load_Only_Selected_Exam_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				await InsertExamAsync("Hidden draft", 0);
				var published = await InsertExamAsync("Published", 1, ExamStatus.Published);
				var marked = await InsertExamAsync("Published marks", 2);
				var mark = new ExamMark(Guid.NewGuid(), marked.Id, student.Id, SubjectId, DivisionId, 75, false, "Well done", TenantId);
				mark.Publish();
				await GetRequiredService<IRepository<ExamMark, Guid>>().InsertAsync(mark, autoSave: true);
				var resulted = await InsertExamAsync("Frozen result", 3);
				var header = new ExamResultHeader(Guid.NewGuid(), resulted.Id, DivisionId, ActorId, TenantId);
				header.Publish(ActorId);
				header.Freeze(ActorId);
				await GetRequiredService<IRepository<ExamResultHeader, Guid>>().InsertAsync(header, autoSave: true);
				await GetRequiredService<IRepository<ExamResultDetail, Guid>>().InsertAsync(new ExamResultDetail(Guid.NewGuid(), header.Id,
					resulted.Id, student.Id, DivisionId, 120, 96, 80, "A", 2, 1, ExamStudentResultStatus.Pass, TenantId), autoSave: true);
				var hiddenOtherStudent = await InsertExamAsync("Other student only", 4);
				var otherMark = new ExamMark(Guid.NewGuid(), hiddenOtherStudent.Id, Guid.NewGuid(), SubjectId, DivisionId, 90, false, tenantId: TenantId);
				otherMark.Publish();
				await GetRequiredService<IRepository<ExamMark, Guid>>().InsertAsync(otherMark, autoSave: true);
				await InsertExamAsync("Other division", 5, ExamStatus.Published, Guid.NewGuid());
				using (Tenant.Change(Guid.NewGuid())) await InsertExamAsync("Other tenant", 0, ExamStatus.Published);

				var db = await GetDbAsync();
				db.ChangeTracker.Clear();
				var page = await Academic360.GetTabAsync(student.Id, "exams", new StudentAcademic360Input { ExamSkipCount = 1, ExamMaxResultCount = 1 });
				page.Exams.Single().ExamId.ShouldBe(marked.Id);
				page.Exams.Single().ObtainedMarks.ShouldBe(75);
				page.Exams.Single().Subjects.Single().MarksObtained.ShouldBe(75d.ToString("F2"));
				db.ChangeTracker.Entries<Exam>().Select(entry => entry.Entity.Id).ShouldBe(new[] { marked.Id });
				db.ChangeTracker.Entries<ExamSubject>().Count().ShouldBe(1);
				var all = await Academic360.GetTabAsync(student.Id, "exams", new StudentAcademic360Input());
				all.Exams.Select(exam => exam.ExamId).ShouldBe(new[] { published.Id, marked.Id, resulted.Id });
				var frozen = all.Exams.Last();
				frozen.ResultAvailable.ShouldBeTrue();
				frozen.TotalMarks.ShouldBe(120);
				frozen.ObtainedMarks.ShouldBe(96);
				frozen.Percentage.ShouldBe(80);
				frozen.Rank.ShouldBe(2);
			}
		});
	}

	[Fact]
	public async Task Learning_Should_Preserve_Historical_Filter_Summary_Paging_And_Submission_Dates_Without_Tracking_Task_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				var homeworkType = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "360 practice");
				var assignmentType = await GetRequiredService<AssignmentTypeManager>().CreateAsync(AcademicMediumId, "360 project",
					defaultMarks: 20, evaluationRequired: true, defaultEvaluationMethod: EvaluationMethod.Marks);
				var today = DateTime.UtcNow.Date;
				var reviewed = await InsertHomeworkAsync(homeworkType.Id, "Reviewed", today.AddDays(3), YearId);
				var pending = await InsertHomeworkAsync(homeworkType.Id, "Pending", today.AddDays(2), YearId);
				await InsertHomeworkAsync(homeworkType.Id, "Late", today.AddDays(-2), YearId);
				await InsertHomeworkAsync(homeworkType.Id, "Other year", today.AddDays(4), Guid.NewGuid());
				await InsertHomeworkAsync(homeworkType.Id, "Unpublished", today.AddDays(5), YearId, published: false);
				var assignment = new AssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Submitted assignment", "Large body is not selected",
					assignmentType.Id, SubmissionType.OnlineText, totalMarks: 20, tenantId: TenantId, academicYearId: YearId, educationMediumId: AcademicMediumId);
				assignment.DivisionSchedules.Add(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, DivisionId,
					today.AddDays(-1), today.AddDays(1), TenantId) { IsPublished = true, PublishedAt = today.AddDays(-1).AddHours(1) });
				await GetRequiredService<IRepository<AssignmentEntity, Guid>>().InsertAsync(assignment, autoSave: true);
				var submittedAt = new DateTimeOffset(2026, 9, 16, 8, 15, 0, TimeSpan.FromMinutes(330));
				await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(),
					reviewed.Id, student.Id, DivisionId, today, TenantId)
				{
					Status = SubmissionStatus.Reviewed, Marks = 8, TotalMarks = 10, Feedback = "Good work",
					SubmissionDate = submittedAt, ReviewedAt = submittedAt.AddHours(1), SubmittedContent = new string('x', 2000)
				}, autoSave: true);
				await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(),
					assignment.Id, student.Id, DivisionId, today, TenantId, isAssignment: true)
				{ Status = SubmissionStatus.Submitted }, autoSave: true);
				var db = await GetDbAsync();
				db.ChangeTracker.Clear();
				var response = await Academic360.GetTabAsync(student.Id, "learning", new StudentAcademic360Input
				{ TaskSkipCount = 1, TaskMaxResultCount = 1, RecentTaskCount = 2 });
				var tasks = response.LearningTasks;
				tasks.TotalAssigned.ShouldBe(4);
				tasks.Reviewed.ShouldBe(1);
				tasks.Pending.ShouldBe(1);
				tasks.Late.ShouldBe(1);
				tasks.Submitted.ShouldBe(3);
				tasks.PendingEvaluation.ShouldBe(2);
				tasks.Tasks.Single().Id.ShouldBe(pending.Id);
				tasks.RecentTasks.Select(task => task.Id).ShouldBe(new[] { reviewed.Id, pending.Id });
				tasks.RecentTasks.First().SubmissionDate.ShouldBe(submittedAt.DateTime);
				tasks.RecentTasks.First().EvaluationDate.ShouldBe(submittedAt.AddHours(1).DateTime);
				tasks.RecentTasks.First().Feedback.ShouldBe("Good work");
				db.ChangeTracker.Entries<HomeworkAssignmentEntity>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AssignmentEntity>().ShouldBeEmpty();
				db.ChangeTracker.Entries<StudentSubmissionEntity>().ShouldBeEmpty();
				var historical = await Academic360.GetTabAsync(student.Id, "learning", new StudentAcademic360Input { IncludeHistorical = true });
				historical.LearningTasks.TotalAssigned.ShouldBe(5);
				historical.LearningTasks.Tasks.Single(task => task.Id == assignment.Id).TotalMarks.ShouldBe(20);
			}
		});
	}

	[Fact]
	public async Task Tabs_Should_Reject_Other_Tenant_And_Users_Without_Student_Scope()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				using (Tenant.Change(Guid.NewGuid()))
					await Should.ThrowAsync<EntityNotFoundException>(() => Academic360.GetTabAsync(student.Id, "learning", new()));
				using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
					[new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString())], "Test"))))
					(await Should.ThrowAsync<BusinessException>(() => Academic360.GetTabAsync(student.Id, "exams", new())))
						.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
		});
	}

	[Fact]
	public async Task Timetable_Tab_Should_Not_Load_Classmates_Student_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				var student = await SeedStudentAsync();
				var repository = GetRequiredService<IRepository<Student, Guid>>();
				for (var index = 0; index < 20; index++)
					await repository.InsertAsync(new Student(Guid.NewGuid(), TenantId, $"Classmate {index}", null, "Student",
						YearId, student.AcademicBoardId!.Value, AcademicMediumId, student.AcademicMediumStageId!.Value,
						StandardId, DivisionId), autoSave: true);
				var db = await GetDbAsync();
				db.ChangeTracker.Clear();
				var response = await Academic360.GetTabAsync(student.Id, "timetable", new());
				response.Student.StudentId.ShouldBe(student.Id);
				db.ChangeTracker.Entries<Student>().Select(entry => entry.Entity.Id).ShouldBe(new[] { student.Id });
			}
		});
	}

	private async Task<Student> SeedStudentAsync()
	{
		await SeedAllocationAsync();
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(YearId, TenantId, "2026-27",
			new DateTime(2026, 6, 1), new DateTime(2027, 5, 31)), autoSave: true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
		return await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new Student(Guid.NewGuid(), TenantId, "Academic", null, "Student",
			YearId, medium.EducationBoardId, AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId), autoSave: true);
	}

	private async Task<Exam> InsertExamAsync(string name, int daysAgo, ExamStatus status = ExamStatus.Pending, Guid? divisionId = null)
	{
		var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), name, StandardId, Tenant.Id) { CreationTime = DateTime.UtcNow.AddDays(-daysAgo) };
		exam.ChangeStatus(status);
		exam.AddDivision(Guid.NewGuid(), divisionId ?? DivisionId);
		exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, SubjectId, new DateTime(2025, 1, 1),
			new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1, 10, 0, 0), 100, 35));
		return await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
	}

	private Task<HomeworkAssignmentEntity> InsertHomeworkAsync(Guid typeId, string title, DateTime due, Guid yearId, bool published = true)
	{
		var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, title, "Large body is not selected", typeId,
			SubmissionType.OnlineText, TenantId, yearId, educationMediumId: AcademicMediumId);
		homework.DivisionSchedules.Add(new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, DivisionId, due.AddDays(-2), due, TenantId)
		{ IsPublished = published, PublishedAt = published ? due.AddDays(-1) : null });
		return GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(homework, autoSave: true);
	}

	private Task<ShikshaMitraDbContext> GetDbAsync() => GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
}
