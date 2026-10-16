using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Content;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public class MobileAcademicAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private async Task<T> Insert<T>(T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		await GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, true);
	private IDisposable SignIn(Guid user) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, user.ToString())], "test")));
	private record Fixture(Student Student, IdentityUser User, StudentPortalAccountLink Link, StudentEnrollment Enrollment, AcademicYear Year, AssignmentEntity Assignment, Exam Exam, Guid SubjectId, Guid ChapterId);
	private async Task<Fixture> Seed(Guid tenantId, bool parent)
	{
		var today = MobileAcademicContextAppService.SchoolToday;
		var year = await Insert(await GetRequiredService<AcademicYearManager>().CreateAsync("Current", today.AddDays(-30), today.AddDays(300)));
		var board = await Insert(await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
		var medium = await Insert(await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
		var stage = await Insert(await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
		var standard = await Insert(await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 8", 12, null));
		var division = await Insert(await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30));
		var student = await Insert(new Student(Guid.NewGuid(), tenantId, "Fixture", "", "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id, rollNo: 12));
		var enrollment = await Insert(StudentEnrollment.Create(Guid.NewGuid(), tenantId, student.Id, year.Id, standard.Id, division.Id, 12));
		var user = new IdentityUser(Guid.NewGuid(), "mobile-fixture", "mobile@example.test", tenantId);
		user.SetEmailConfirmed(true);
		await Insert(user);
		var link = await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, student.Id, user.Id, parent ? "guardian" : "student", parent ? "mother" : "self", Guid.NewGuid()));
		var subject = await Insert(new Subject(Guid.NewGuid(), tenantId, "Mathematics", "#008967", medium.Id));
		var assigned = await Insert(new AssignedSubject(Guid.NewGuid(), tenantId, standard.Id, subject.Id, "MATH", SubjectStatus.Published));
		var chapter = await Insert(new AssignedSubjectChapter(Guid.NewGuid(), assigned.Id, 1, "Fractions", today, today.AddDays(10))
			{ AssignedSubjectId = assigned.Id, TenantId = tenantId, PublishStatus = "Published", Visibility = "All", ParentSummary = "School family guidance" });
		var type = await GetRequiredService<AssignmentTypeManager>().CreateAsync(medium.Id, "Practice", defaultMarks: 20, defaultEvaluationMethod: EvaluationMethod.Marks);
		var assignment = new AssignmentEntity(Guid.NewGuid(), standard.Id, subject.Id, "Practice fractions", "Solve the supplied questions.", type.Id, SubmissionType.OnlineText, 20, tenantId, year.Id);
		assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, division.Id, today, DateTime.UtcNow.AddDays(3), tenantId));
		assignment.PublishAll();
		await Insert(assignment);
		var examType = await Insert(new ExamType(Guid.NewGuid(), tenantId, "Unit test", null, medium.Id));
		var exam = new Exam(Guid.NewGuid(), examType.Id, "Fractions test", standard.Id, tenantId);
		exam.AddDivision(Guid.NewGuid(), division.Id);
		exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id, today.AddDays(-1), today.AddHours(9), today.AddHours(10), 20, 7));
		exam.ChangeStatus(ExamStatus.Completed);
		await Insert(exam);
		return new(student, user, link, enrollment, year, assignment, exam, subject.Id, chapter.Id);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Student_and_guardian_read_published_academics_without_staff_grants(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		(await GetRequiredService<IMobileAcademicContextAppService>().GetAsync(input)).Student.Id.ShouldBe(f.Student.Id);
		(await GetRequiredService<IMobileHomeworkAppService>().GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id })).Items.Single().Title.ShouldBe("Practice fractions");
		(await GetRequiredService<IMobileSyllabusAppService>().GetChapterAsync(f.ChapterId, input)).ChapterName.ShouldBe("Fractions");
		(await GetRequiredService<IMobileSyllabusAppService>().GetChapterAsync(f.ChapterId, input)).ParentSummary.ShouldBe(parent ? "School family guidance" : null);
		var syllabusSubject = (await GetRequiredService<IMobileSyllabusAppService>().GetDashboardAsync(input)).Subjects.Single();
		syllabusSubject.Id.ShouldBe(f.SubjectId);
		syllabusSubject.Name.ShouldBe("Mathematics");
		syllabusSubject.Color.ShouldBe("#008967");
		(await GetRequiredService<IMobileAttendanceAppService>().GetOverviewAsync(input)).CurrentPercentage.ShouldBeNull();
		(await GetRequiredService<IMobileExamAppService>().GetAsync(f.Exam.Id, input)).Schedule.Single().SubjectId.ShouldBe(f.SubjectId);
		(await GetRequiredService<IMobileResultAppService>().GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id })).Items.Single().Status.ShouldBe("pending");
		(await GetRequiredService<IMobileCalendarAppService>().GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Year = DateTime.Today.Year, Month = DateTime.Today.Month })).ShouldBeEmpty();
		(await GetRequiredService<IMobileDashboardAppService>().GetAsync(input)).PendingHomework.ShouldBe(1);
		var chapter = await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().GetAsync(f.ChapterId);
		chapter.Visibility = "Parents";
		await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().UpdateAsync(chapter, true);
		if (parent) (await GetRequiredService<IMobileSyllabusAppService>().GetChapterAsync(f.ChapterId, input)).ChapterName.ShouldBe("Fractions");
		else await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => GetRequiredService<IMobileSyllabusAppService>().GetChapterAsync(f.ChapterId, input));
	});

	[Theory]
	[InlineData(false, "Draft", "Teacher", false)]
	[InlineData(true, "Draft", "All", false)]
	[InlineData(false, "In Review", "All", false)]
	[InlineData(true, "Archived", "All", false)]
	[InlineData(false, "Published", "Teacher", false)]
	[InlineData(true, "Published", "Teacher", false)]
	[InlineData(false, "Published", "All", true)]
	[InlineData(true, "Published", "All", true)]
	[InlineData(false, "Published", "Students", true)]
	[InlineData(true, "Published", "Parents", true)]
	[InlineData(false, "Published", "Parents", false)]
	public Task Syllabus_publication_and_audience_apply_to_dashboard_list_and_details(
		bool parent, string publishStatus, string visibility, bool visible) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var repository = GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
		var chapter = await repository.GetAsync(f.ChapterId);
		chapter.PublishStatus = publishStatus;
		chapter.Visibility = visibility;
		await repository.UpdateAsync(chapter, true);
		var service = GetRequiredService<IMobileSyllabusAppService>();
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		var dashboard = await service.GetDashboardAsync(input);
		dashboard.TotalChapters.ShouldBe(visible ? 1 : 0);
		dashboard.Subjects.Single().TotalChapters.ShouldBe(visible ? 1 : 0);
		var list = await service.GetChaptersAsync(f.SubjectId, new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		list.TotalCount.ShouldBe(visible ? 1 : 0);
		if (visible)
		{
			list.Items.Single().Id.ShouldBe(f.ChapterId);
			list.Items.Single().CoverageStatus.ShouldBe("notStarted");
			(await service.GetChapterAsync(f.ChapterId, input)).Id.ShouldBe(f.ChapterId);
		}
		else
		{
			list.Items.ShouldBeEmpty();
			await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => service.GetChapterAsync(f.ChapterId, input));
		}
	});

	[Fact]
	public Task Every_module_denies_unlinked_student_and_revoked_link() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = SignIn(f.User.Id);
		async Task Denied(Guid studentId)
		{
			var input = new MobileAcademicInput { StudentId = studentId, AcademicYearId = f.Year.Id };
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileAcademicContextAppService>().GetAsync(input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileHomeworkAppService>().GetAsync(f.Assignment.Id, input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileSyllabusAppService>().GetChapterAsync(f.ChapterId, input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileAttendanceAppService>().GetOverviewAsync(input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileExamAppService>().GetAsync(f.Exam.Id, input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileResultAppService>().GetYearsAsync(input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileDashboardAppService>().GetAsync(input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileCalendarAppService>().GetMarkerAsync(Guid.NewGuid(), input));
			await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IMobileHomeworkAppService>().SaveAsync(f.Assignment.Id, new() { StudentId = studentId, AcademicYearId = f.Year.Id, TextAnswer = "Forged" }, false, false));
		}
		await Denied(Guid.NewGuid());
		await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Denied(f.Student.Id);
	});

	[Fact]
	public Task Draft_and_submission_persist_and_reviewed_work_cannot_be_overwritten() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswer = "Draft response" };
		(await service.SaveAsync(f.Assignment.Id, input, true, false)).Status.ShouldBe("draft");
		input.TextAnswer = "Submitted response";
		(await service.SaveAsync(f.Assignment.Id, input, false, false)).Status.ShouldBe("submittedEditable");
		(await service.GetAsync(f.Assignment.Id, input)).TextAnswer.ShouldBe("Submitted response");
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, false, false));
		var repository = GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		var submission = await repository.FindAsync(x => x.AssignmentId == f.Assignment.Id && x.StudentId == f.Student.Id);
		submission!.UpdateFeedback(18, 20, "Reviewed", null, SubmissionStatus.Reviewed, f.User.Id);
		await repository.UpdateAsync(submission, true);
		await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(f.Assignment.Id, input, false, true));
	});

	[Fact]
	public Task Unpublished_and_withheld_results_never_expose_marks_or_report_cards() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = SignIn(f.User.Id);
		var header = await Insert(new ExamResultHeader(Guid.NewGuid(), f.Exam.Id, f.Enrollment.DivisionId!.Value, f.User.Id, tenantId));
		var detail = await Insert(new ExamResultDetail(Guid.NewGuid(), header.Id, f.Exam.Id, f.Student.Id, f.Enrollment.DivisionId.Value, 20, 18, 90, "A", 1, 1, ExamStudentResultStatus.Pass, tenantId));
		var service = GetRequiredService<IMobileResultAppService>();
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		(await service.GetAsync(f.Exam.Id, input)).OverallObtained.ShouldBeNull();
		header.Publish(f.User.Id); await GetRequiredService<IRepository<ExamResultHeader, Guid>>().UpdateAsync(header, true);
		(await service.GetAsync(header.Id, input)).OverallObtained.ShouldBe(18);
		QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
		var report = await service.DownloadReportCardAsync(header.Id, input);
		report.ContentType.ShouldBe("application/pdf");
		Encoding.ASCII.GetString(report.Content.Take(5).ToArray()).ShouldBe("%PDF-");
		detail.MarkAsWithheld(); await GetRequiredService<IRepository<ExamResultDetail, Guid>>().UpdateAsync(detail, true);
		var hidden = await service.GetAsync(header.Id, input);
		hidden.Status.ShouldBe("unavailable"); hidden.OverallObtained.ShouldBeNull(); hidden.Subjects.ShouldBeEmpty();
		await Should.ThrowAsync<BusinessException>(() => service.DownloadReportCardAsync(header.Id, input));
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Submitted_legacy_attendance_uses_header_date_and_only_guardians_write_reasons(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var date = MobileAcademicContextAppService.SchoolToday;
		var manager = GetRequiredService<AttendanceManager>();
		var day = await manager.GetOrCreateAttendanceDayAsync(f.Year.Id, f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value, date);
        await manager.MarkAttendanceAsync(day.Id, [new AttendanceManager.AttendanceEntryInput { StudentId = f.Student.Id, Status = AttendanceStatus.Absent }]);
		day.SaveRevision(Guid.NewGuid(), "fixture", true, "fixture", "{}");
		await GetRequiredService<IRepository<AttendanceDay, Guid>>().UpdateAsync(day, true);
		var service = GetRequiredService<IMobileAttendanceAppService>();
		var calendar = await service.GetCalendarAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Year = date.Year, Month = date.Month });
		calendar.Single(x => x.Date == DateOnly.FromDateTime(date)).Status.ShouldBe("absent");
		var reason = new MobileAbsenceInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Reason = "Medical appointment" };
		if (parent) (await service.SetAbsenceReasonAsync(DateOnly.FromDateTime(date), reason)).Reason.ShouldBe("Medical appointment");
		else await Should.ThrowAsync<AbpAuthorizationException>(() => service.SetAbsenceReasonAsync(DateOnly.FromDateTime(date), reason));
	});

	[Fact]
	public Task Guardian_leave_uses_school_calendar_persists_half_day_times_and_locks_after_decision() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = SignIn(f.User.Id);
		var date = MobileAcademicContextAppService.SchoolToday.AddDays(1);
		var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, f.Student.AcademicMediumId, f.Year.Id, "School calendar", f.Year.StartDate, f.Year.EndDate, isDefault: true);
		calendar.Publish(f.User.Id); await Insert(calendar);
		await Insert(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id, date, DayType.WorkingDay, "Teaching day"));
		await Insert(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id, date.AddDays(1), DayType.Holiday, "School holiday"));
		var type = await Insert(await GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Medical", "#123456"));
		var service = GetRequiredService<IMobileAttendanceAppService>();
		var input = new MobileLeaveInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, LeaveTypeId = type.Id, StartDate = DateOnly.FromDateTime(date), EndDate = DateOnly.FromDateTime(date.AddDays(1)), Description = "Medical appointment" };
		var saved = await service.SaveLeaveAsync(null, input);
		saved.DurationDays.ShouldBe(1); saved.Status.ShouldBe("pending"); saved.CanEdit.ShouldBeTrue();
		await Should.ThrowAsync<BusinessException>(() => service.SaveLeaveAsync(null, input));
		(await service.CancelLeaveAsync(saved.Id, input)).Status.ShouldBe("cancelled");
		input.EndDate = input.StartDate; input.IsHalfDay = true; input.HalfDayStartTime = new(8, 30); input.HalfDayEndTime = new(12, 30);
		var half = await service.SaveLeaveAsync(null, input);
		half.DurationDays.ShouldBe(.5m); half.HalfDayStartTime.ShouldBe(new TimeOnly(8, 30));
		var repository = GetRequiredService<IRepository<StudentLeave, Guid>>();
		var leave = await repository.GetAsync(half.Id); leave.StandardId.ShouldBe(f.Enrollment.StandardId);
		leave.Approve("Approved by school"); await repository.UpdateAsync(leave, true);
		(await service.GetLeaveAsync(half.Id, input)).CanCancel.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.CancelLeaveAsync(half.Id, input));
		await Should.ThrowAsync<BusinessException>(() => service.SaveLeaveAsync(half.Id, input));
	});

	[Fact]
	public Task Attachment_upload_rejects_foreign_references_and_cross_tenant_blob_paths() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		var service = GetRequiredService<IMobileHomeworkAppService>();
		using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture"));
		var upload = await service.UploadAsync(f.Assignment.Id, input, new RemoteStreamContent(stream, "answer.pdf", "application/pdf"));
		var download = await service.DownloadAsync(f.Assignment.Id, upload.Id, input);
		download.Name.ShouldBe("answer.pdf"); download.Content.ShouldBe(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture"));
		var chapter = await GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>().GetAsync(f.ChapterId);
		var document = await Insert(new AssignedSubjectDocument(Guid.NewGuid(), chapter.AssignedSubjectId, chapter.Id, upload.Id.ToString("N") + ".pdf") { TenantId = tenantId });
		var syllabus = GetRequiredService<IMobileSyllabusAppService>();
		(await syllabus.GetChapterAsync(chapter.Id, input)).Resources.Single().Id.ShouldBe(document.Id);
		(await syllabus.DownloadAsync(chapter.Id, document.Id, input)).Content.ShouldBe(download.Content);
		var store = GetRequiredService<MobileAttachmentStore>();
		await Should.ThrowAsync<BusinessException>(() => store.ResolveAsync(f.Student.Id, Guid.NewGuid(), [upload.Id]));
		await Should.ThrowAsync<BusinessException>(() => store.DownloadPublishedAsync($"files/{Guid.NewGuid()}/answer.pdf", "answer.pdf", "application/pdf"));
		await Should.ThrowAsync<BusinessException>(() => store.DownloadPublishedAsync("../answer.pdf", "answer.pdf", "application/pdf"));
	});
}
