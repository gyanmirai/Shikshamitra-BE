using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData(0)]
	[InlineData(-2)]
	public async Task Daily_attendance_matches_active_directory_on_current_and_past_dates(int offset)
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "ActiveDirectoryDates");
		var active = await CreateEnrolledStudentAsync(context, "DirectoryActive", division);
		var inactive = await CreateEnrolledStudentAsync(context, "DirectoryInactive", division);
		var left = await CreateEnrolledStudentAsync(context, "DirectoryLeft", division);
		var incomplete = await CreateEnrolledStudentAsync(context, "DirectoryIncomplete", division, directoryEligible: false);
		await SetAttendanceFixtureStatusAsync(inactive.StudentId, StudentLifeCycleStatusEnum.InActive);
		await SetAttendanceFixtureStatusAsync(left.StudentId, StudentLifeCycleStatusEnum.Left);
		var date = context.ReferenceDate.AddDays(offset);
		await EnsureWorkingDateAsync(context, date);

		var roster = await ReadRosterAsync(context, division, date);
		roster.Students.Select(s => s.StudentId).ShouldBe(new[] { active.StudentId });
		roster.TotalStudents.ShouldBe(1);
		await AssertDailyProjectionMatchesRosterAsync(context, division, date, roster);
		var response = await Client.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={date:yyyy-MM-dd}&divisionId={division}&maxResultCount=100");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var workspace = (await response.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		workspace.TotalCount.ShouldBe(1);
		workspace.Items.Single().StudentId.ShouldBe(active.StudentId);

		foreach (var hidden in new[] { inactive.StudentId, left.StudentId, incomplete.StudentId })
		{
			var denied = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark",
				Input(context, division, date, roster, hidden, AttendanceStatus.Present), JsonOptions);
			denied.IsSuccessStatusCode.ShouldBeFalse();
			(await denied.Content.ReadAsStringAsync()).ShouldContain("StudentOutsideDatedRoster");
		}
		(await ReadRosterAsync(context, division, date)).AttendanceDayId.ShouldBeNull();
	}

	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData(0, true)]
	[InlineData(-2, true)]
	[InlineData(0, false)]
	[InlineData(-2, false)]
	public async Task Daily_attendance_hides_submitted_students_outside_active_directory_but_keeps_reports_and_history(int offset, bool inactive)
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "SubmittedDirectoryVisibility");
		var active = await CreateEnrolledStudentAsync(context, "SubmittedStillActive", division);
		var hidden = await CreateEnrolledStudentAsync(context, "SubmittedHidden", division);
		var date = context.ReferenceDate.AddDays(offset);
		await SubmitAllAsync(context, division, date);
		var before = await ReadAttendancePersistenceSnapshotAsync(context, division, date, hidden.StudentId);
		if (inactive) await SetAttendanceFixtureStatusAsync(hidden.StudentId, StudentLifeCycleStatusEnum.InActive);
		else await RemoveAttendanceFixtureDirectoryApprovalAsync(hidden.StudentId);

		var roster = await ReadRosterAsync(context, division, date);
		roster.IsSubmitted.ShouldBeTrue();
		roster.Students.Select(s => s.StudentId).ShouldBe(new[] { active.StudentId });
		roster.TotalStudents.ShouldBe(1);
		await AssertDailyProjectionMatchesRosterAsync(context, division, date, roster);
		var denied = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark",
			Input(context, division, date, roster, hidden.StudentId, AttendanceStatus.Present), JsonOptions);
		denied.IsSuccessStatusCode.ShouldBeFalse();
		(await denied.Content.ReadAsStringAsync()).ShouldContain("StudentOutsideDatedRoster");
		if (offset == 0)
		{
			var update = Input(context, division, date, roster, active.StudentId, AttendanceStatus.Absent);
			update.CorrectionReason = "Verified visible student's absence";
			update.Submit = true;
			var saved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", update, JsonOptions);
			saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		}
		await AssertAttendanceHistoryAndRegisterAsync(context, division, date, hidden.StudentId, AttendanceStatus.Present);
		(await ReadAttendancePersistenceSnapshotAsync(context, division, date, hidden.StudentId)).ShouldBe(before);
	}

	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Daily_attendance_visible_submission_preserves_hidden_drafts_and_submitted_snapshots(bool pending)
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "HiddenAttendancePreservation");
		var active = await CreateEnrolledStudentAsync(context, "PreservedVisible", division);
		var hidden = await CreateEnrolledStudentAsync(context, "PreservedHidden", division);
		var date = context.ReferenceDate;
		await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date);
		var hiddenStatus = pending ? AttendanceStatus.Pending : AttendanceStatus.Present;
		var draft = Input(context, division, date, roster, hidden.StudentId, hiddenStatus);
		draft.Entries[0].HalfDay = !pending;
		draft.Entries[0].Remarks = "Original saved draft must remain";
		var saved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", draft, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var hiddenDraft = await ReadAttendancePersistenceSnapshotAsync(context, division, date, hidden.StudentId);
		await SetAttendanceFixtureStatusAsync(hidden.StudentId, StudentLifeCycleStatusEnum.InActive);

		roster = await ReadRosterAsync(context, division, date);
		roster.Students.Select(s => s.StudentId).ShouldBe(new[] { active.StudentId });
		var submit = Input(context, division, date, roster, active.StudentId, AttendanceStatus.Present);
		submit.Submit = true;
		var submitted = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", submit, JsonOptions);
		submitted.StatusCode.ShouldBe(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
		var afterSubmission = await ReadAttendancePersistenceSnapshotAsync(context, division, date, hidden.StudentId);
		afterSubmission.Entry.ShouldBe(hiddenDraft.Entry);

		roster = await ReadRosterAsync(context, division, date);
		roster.NeedsReview.ShouldBeFalse();
		await AssertDailyProjectionMatchesRosterAsync(context, division, date, roster);
		var correction = Input(context, division, date, roster, active.StudentId, AttendanceStatus.Absent);
		correction.CorrectionReason = "Verified classroom observation";
		correction.Entries[0].Remarks = "Corrected visible student only";
		correction.Submit = true;
		var corrected = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", correction, JsonOptions);
		corrected.StatusCode.ShouldBe(HttpStatusCode.OK, await corrected.Content.ReadAsStringAsync());
		(await ReadAttendancePersistenceSnapshotAsync(context, division, date, hidden.StudentId)).ShouldBe(afterSubmission);
		await AssertAttendanceHistoryAndRegisterAsync(context, division, date, hidden.StudentId, hiddenStatus, halfDay: !pending);
		(await ReadRosterAsync(context, division, date)).Students.Single().Status.ShouldBe(AttendanceStatus.Absent);
	}

	private async Task AssertDailyProjectionMatchesRosterAsync(SeededDivisionContext context, Guid division, DateTime date,
		StudentAttendanceListDto roster)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
			new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)], "Test"))))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var state = (await services.GetRequiredService<AttendanceDailyRegisterProjection>()
				.ReadAsync(context.AcademicYearId, [(context.StandardId, division)], date)).Single();
			state.ExpectedStudents.ShouldBe(roster.TotalStudents);
			state.Status.ShouldBe(roster.IsSubmitted ? roster.NeedsReview ? "review" : "submitted" : roster.AttendanceDayId.HasValue ? "draft" : "missing");
			await uow.CompleteAsync();
		}
	}

	private async Task SetAttendanceFixtureStatusAsync(Guid studentId, StudentLifeCycleStatusEnum status)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var students = services.GetRequiredService<IStudentRepository>();
			var student = await students.GetInquiryForUpdateAsync(studentId);
			student.ShouldNotBeNull();
			student.ChangeStatus(status);
			await students.UpdateAsync(student);
			await uow.CompleteAsync();
		}
	}

	private async Task RemoveAttendanceFixtureDirectoryApprovalAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await services.GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>()
				.DeleteAsync(o => o.StudentId == studentId);
			await uow.CompleteAsync();
		}
	}

	private async Task<(string Entry, string? SubmittedRoster, string? Policy, string EnrollmentStamp)> ReadAttendancePersistenceSnapshotAsync(
		SeededDivisionContext context, Guid division, DateTime date, Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var day = (await services.GetRequiredService<IAttendanceDayRepository>()
				.FindByDateAndDivisionAsync(context.AcademicYearId, context.StandardId, division, date, includeEntries: true))!;
			var entry = day.Entries.Single(e => e.StudentId == studentId);
			var persisted = JsonSerializer.Serialize(new {
				entry.Id, entry.StudentId, entry.EnrollmentId, entry.AttendanceDayId, entry.SchoolDate,
				entry.Status, entry.Late, entry.HalfDay, entry.Remarks, entry.GuardianAbsenceReason,
				entry.PresentCredit, entry.DenominatorCredit, entry.ExcludedFromPercentage, entry.ApprovedLeaveId,
				entry.PolicySnapshotJson, entry.ExpectedAtSubmission, entry.HistoricalRollNo, entry.MarkedByUserId,
				entry.CreationTime, entry.LastModificationTime
			});
			var enrollment = await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(entry.EnrollmentId!.Value);
			await uow.CompleteAsync();
			return (persisted, day.SubmittedRosterRevision, day.PolicySnapshotJson, enrollment.ConcurrencyStamp);
		}
	}

	private async Task AssertAttendanceHistoryAndRegisterAsync(SeededDivisionContext context, Guid division, DateTime date,
		Guid studentId, AttendanceStatus status, bool halfDay = false)
	{
		var historyResponse = await Client.GetAsync($"{AttendanceBaseUrl}/history?studentId={studentId}&startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = (await historyResponse.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions))!;
		history.TotalCount.ShouldBe(1);
		history.Items.Single().Status.ShouldBe(status);
		var reportResponse = await Client.GetAsync($"{AttendanceBaseUrl}/register?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={division}&startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}&maxResultCount=100");
		reportResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reportResponse.Content.ReadAsStringAsync());
		var register = (await reportResponse.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!;
		var row = register.Rows.Single(r => r.StudentId == studentId);
		row.RecordedDays.ShouldBe(status == AttendanceStatus.Pending ? 0 : 1);
		row.PresentCredit.ShouldBe(status is AttendanceStatus.Absent or AttendanceStatus.Pending ? 0d : halfDay ? .5d : 1d);
	}
}
