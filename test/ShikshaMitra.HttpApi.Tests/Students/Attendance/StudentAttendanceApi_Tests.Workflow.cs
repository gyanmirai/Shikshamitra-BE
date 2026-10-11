using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Retroactive_approved_leave_changes_submitted_credits_only_after_separate_audited_correction_approval()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "RetroactiveLeave");
		var date = context.ReferenceDate.AddDays(-2);
		var student = await CreateEnrolledStudentAsync(context, "RetroactiveLeave", division, attendanceStartDate: date);
		var before = await SubmitAbsentAsync(context, division, date, student.StudentId);
		before.Policy.ApprovedLeaveTreatment.ShouldBe("Exclude"); before.Policy.PriorDayCorrectionRequiresApproval.ShouldBeTrue();
		var entryBefore = await ReadEntrySnapshotAsync(before.AttendanceDayId!.Value, student.StudentId);
		var reportBefore = await ReadSingleDaySummaryAsync(context, student.StudentId, date);
		reportBefore.Percentage.ShouldBe(0d); reportBefore.RecordedDays.ShouldBe(1); reportBefore.DenominatorDays.ShouldBe(1d);
		var leaveId = await CreateAndApproveRetroactiveLeaveAsync(student.StudentId, date);
		var afterLeave = await ReadRosterAsync(context, division, date);
		afterLeave.Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Absent);
		afterLeave.Students.Single(s => s.StudentId == student.StudentId).NeedsCreditCorrection.ShouldBeTrue();
		afterLeave.Revision.ShouldBe(before.Revision);
		(await ReadEntrySnapshotAsync(before.AttendanceDayId.Value, student.StudentId)).ShouldBe(entryBefore);
		var unchanged = await ReadSingleDaySummaryAsync(context, student.StudentId, date);
		unchanged.Percentage.ShouldBe(reportBefore.Percentage); unchanged.RecordedDays.ShouldBe(reportBefore.RecordedDays);
		unchanged.PresentCredit.ShouldBe(reportBefore.PresentCredit); unchanged.DenominatorDays.ShouldBe(reportBefore.DenominatorDays);
		var ordinarySave = Input(context, division, date, afterLeave, student.StudentId, AttendanceStatus.Absent);
		var blockedSave = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", ordinarySave, JsonOptions);
		blockedSave.IsSuccessStatusCode.ShouldBeFalse();
		(await blockedSave.Content.ReadAsStringAsync()).ShouldContain("StudentAttendance:CreditCorrectionRequired");
		var other = await CreateEnrolledStudentAsync(context, "RetroactiveOmittedRow", division, attendanceStartDate: date);
		var submitRoster = await ReadRosterAsync(context, division, date);
		submitRoster.Students.Single(s => s.StudentId == other.StudentId).IsExpected.ShouldBeTrue();
		var omittedRowSubmit = Input(context, division, date, submitRoster, other.StudentId, AttendanceStatus.Present);
		omittedRowSubmit.Submit = true;
		var blockedSubmit = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", omittedRowSubmit, JsonOptions);
		blockedSubmit.IsSuccessStatusCode.ShouldBeFalse();
		(await blockedSubmit.Content.ReadAsStringAsync()).ShouldContain("StudentAttendance:CreditCorrectionRequired");
		(await ReadEntrySnapshotAsync(before.AttendanceDayId.Value, student.StudentId)).ShouldBe(entryBefore);
		afterLeave = await ReadRosterAsync(context, division, date);
		afterLeave.Revision.ShouldBe(before.Revision);
		afterLeave.Students.Single(s => s.StudentId == other.StudentId).Status.ShouldBe(AttendanceStatus.Pending);
		var correction = await RequestRetroactiveCorrectionAsync(context, division, date, student.StudentId, afterLeave, AttendanceStatus.Absent);
		correction.Status.ShouldBe("Pending"); correction.BeforeStatus.ShouldBe(AttendanceStatus.Absent);
		correction.RequestedStatus.ShouldBe(AttendanceStatus.Absent);
		correction.IsCreditCorrection.ShouldBeTrue();
		correction.BeforePresentCredit.ShouldBe(0d); correction.BeforeDenominatorCredit.ShouldBe(1d);
		correction.RequestedPresentCredit.ShouldBe(0d); correction.RequestedDenominatorCredit.ShouldBe(0d);
		(await ReadEntrySnapshotAsync(before.AttendanceDayId.Value, student.StudentId)).ShouldBe(entryBefore);
		var reviewerId = Guid.NewGuid(); await SeedDivisionScopeAsync(reviewerId, division, context.AcademicYearId);
		using var reviewer = CreateClientAs(reviewerId);
		var approved = await reviewer.PostAsJsonAsync($"{AttendanceBaseUrl}/corrections/{correction.Id}/approve",
			new AttendanceCorrectionDecisionInput { Revision = correction.Revision, Reason = "Reviewed retroactive approved leave and recorded credits" }, JsonOptions);
		approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
		(await approved.Content.ReadFromJsonAsync<AttendanceCorrectionDto>(JsonOptions))!.Status.ShouldBe("Applied");
		var corrected = await ReadRosterAsync(context, division, date);
		corrected.IsSubmitted.ShouldBeTrue(); corrected.Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Absent);
		corrected.Students.Single(s => s.StudentId == student.StudentId).NeedsCreditCorrection.ShouldBeFalse();
		long.Parse(corrected.Revision).ShouldBeGreaterThan(long.Parse(before.Revision));
		var entryAfter = await ReadEntrySnapshotAsync(before.AttendanceDayId.Value, student.StudentId);
		entryAfter.ApprovedLeaveId.ShouldBe(leaveId); entryAfter.Excluded.ShouldBeTrue(); entryAfter.Denominator.ShouldBe(0d);
		entryAfter.Present.ShouldBe(0d); entryAfter.Policy.ShouldBe(entryBefore.Policy);
		var finalReport = await ReadSingleDaySummaryAsync(context, student.StudentId, date);
		finalReport.DenominatorDays.ShouldBe(0d); finalReport.Percentage.ShouldBeNull(); finalReport.MissingDays.ShouldBe(0);
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var audit = await services.GetRequiredService<IRepository<StudentAttendanceCorrection, Guid>>().GetAsync(correction.Id);
			audit.RequestedBy.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId)); audit.ReviewedBy.ShouldBe(reviewerId);
			audit.Status.ShouldBe("Applied"); audit.ReviewReason.ShouldBe("Reviewed retroactive approved leave and recorded credits");
			await uow.CompleteAsync();
		}
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Correction_approval_rejects_changed_leave_source_without_mutating_submitted_record_or_pending_request()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "StaleLeaveCorrection");
		var date = context.ReferenceDate.AddDays(-3);
		var student = await CreateEnrolledStudentAsync(context, "StaleLeaveCorrection", division, attendanceStartDate: date);
		var before = await SubmitAbsentAsync(context, division, date, student.StudentId);
		var entryBefore = await ReadEntrySnapshotAsync(before.AttendanceDayId!.Value, student.StudentId);
		var correction = await RequestRetroactiveCorrectionAsync(context, division, date, student.StudentId, before, AttendanceStatus.Present);
		correction.Status.ShouldBe("Pending");
		correction.IsCreditCorrection.ShouldBeFalse();
		correction.BeforePresentCredit.ShouldBe(0d); correction.BeforeDenominatorCredit.ShouldBe(1d);
		correction.RequestedPresentCredit.ShouldBe(1d); correction.RequestedDenominatorCredit.ShouldBe(1d);
		await CreateAndApproveRetroactiveLeaveAsync(student.StudentId, date);
		var listResponse = await Client.GetAsync($"{AttendanceBaseUrl}/corrections?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={division}&maxResultCount=20");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var listed = (await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AttendanceCorrectionDto>>(JsonOptions))!.Items.Single(c => c.Id == correction.Id);
		listed.Status.ShouldBe("Pending"); listed.IsCreditCorrection.ShouldBeFalse();
		listed.BeforeStatus.ShouldBe(AttendanceStatus.Absent); listed.RequestedStatus.ShouldBe(AttendanceStatus.Present);
		listed.BeforePresentCredit.ShouldBe(0d); listed.BeforeDenominatorCredit.ShouldBe(1d);
		listed.RequestedPresentCredit.ShouldBe(1d); listed.RequestedDenominatorCredit.ShouldBe(1d);
		var reviewerId = Guid.NewGuid(); await SeedDivisionScopeAsync(reviewerId, division, context.AcademicYearId);
		using var reviewer = CreateClientAs(reviewerId);
		var approval = await reviewer.PostAsJsonAsync($"{AttendanceBaseUrl}/corrections/{correction.Id}/approve",
			new AttendanceCorrectionDecisionInput { Revision = correction.Revision, Reason = "Review after source leave changed" }, JsonOptions);
		approval.IsSuccessStatusCode.ShouldBeFalse();
		(await approval.Content.ReadAsStringAsync()).ShouldContain(AttendanceOperationErrorCodes.StaleCorrection);
		(await ReadEntrySnapshotAsync(before.AttendanceDayId.Value, student.StudentId)).ShouldBe(entryBefore);
		var after = await ReadRosterAsync(context, division, date);
		after.Revision.ShouldBe(before.Revision); after.Students.Single().NeedsCreditCorrection.ShouldBeTrue();
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var pending = await services.GetRequiredService<IRepository<StudentAttendanceCorrection, Guid>>().GetAsync(correction.Id);
			pending.Status.ShouldBe("Pending"); pending.ConcurrencyStamp.ShouldBe(correction.Revision); pending.ReviewedBy.ShouldBeNull();
			await uow.CompleteAsync();
		}
	}

	private async Task<StudentAttendanceListDto> SubmitAbsentAsync(SeededDivisionContext context, Guid division, DateTime date, Guid studentId)
	{
		await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date);
		var input = Input(context, division, date, roster, studentId, AttendanceStatus.Absent); input.Submit = true;
		// POST carries a local offset while the preceding GET uses the school-date query format.
		input.Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
		var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await ReadRosterAsync(context, division, date);
	}

	private async Task<AttendanceCorrectionDto> RequestRetroactiveCorrectionAsync(SeededDivisionContext context, Guid division, DateTime date,
		Guid studentId, StudentAttendanceListDto roster, AttendanceStatus status)
	{
		var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/corrections", new AttendanceCorrectionInput {
			AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = division, Date = date,
			StudentId = studentId, Status = status, Revision = roster.Revision, OperationId = Guid.NewGuid(),
			Reason = "Reconcile recorded attendance with approved retroactive leave" }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AttendanceCorrectionDto>(JsonOptions))!;
	}

	private async Task<Guid> CreateAndApproveRetroactiveLeaveAsync(Guid studentId, DateTime date)
	{
		var typeResponse = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/settings/leave-types",
			new CreateUpdateStudentLeaveTypeDto { LeaveTypeName = $"Retro {Guid.NewGuid():N}"[..20], Color = "#2F80ED", IsActive = true }, JsonOptions);
		typeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await typeResponse.Content.ReadAsStringAsync());
		var type = (await typeResponse.Content.ReadFromJsonAsync<StudentLeaveTypeDto>(JsonOptions))!;
		var schoolDate = new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(5.5));
		var created = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/leaves", new CreateStudentLeaveDto {
			StudentId = studentId, StudentLeaveTypeId = type.Id, LeaveDate = schoolDate, EndDate = schoolDate,
			IsHalfDay = false, Description = "Retroactive medical leave after submitted attendance" }, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var leave = (await created.Content.ReadFromJsonAsync<StudentLeaveDto>(JsonOptions))!;
		var approved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/leaves/{leave.Id}/approve",
			new ApproveRejectStudentLeaveDto { Status = StudentLeaveStatusEnum.Approved, Remarks = "Approved medical evidence" }, JsonOptions);
		approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
		return leave.Id;
	}

	private async Task<AttendanceSummaryDto> ReadSingleDaySummaryAsync(SeededDivisionContext context, Guid studentId, DateTime date)
	{
		var response = await Client.GetAsync($"{AttendanceBaseUrl}/summary?academicYearId={context.AcademicYearId}&studentId={studentId}&startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AttendanceSummaryDto>(JsonOptions))!;
	}

	private async Task<RecordedEntrySnapshot> ReadEntrySnapshotAsync(Guid dayId, Guid studentId)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var entry = await services.GetRequiredService<IAttendanceEntryRepository>().GetAsync(e => e.AttendanceDayId == dayId && e.StudentId == studentId);
			var result = new RecordedEntrySnapshot(entry.Status, entry.Late, entry.HalfDay, entry.Remarks, entry.PresentCredit,
				entry.DenominatorCredit, entry.ExcludedFromPercentage, entry.ApprovedLeaveId, entry.PolicySnapshotJson,
				entry.ExpectedAtSubmission, entry.MarkedByUserId, entry.LastModificationTime);
			await uow.CompleteAsync(); return result;
		}
	}
	private sealed record RecordedEntrySnapshot(AttendanceStatus Status, bool Late, bool HalfDay, string? Remarks,
		double? Present, double Denominator, bool Excluded, Guid? ApprovedLeaveId, string? Policy, bool? Expected,
		Guid MarkedBy, DateTime? ModifiedAt);

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Register_count_and_rows_exclude_historical_membership_that_ended_before_the_requested_range()
	{
		var context = await ResolveSeededDivisionContextAsync();
		var originalDivision = await CreateDivisionAsync(context.StandardId, "EndedHistorical");
		var currentDivision = await CreateDivisionAsync(context.StandardId, "EndedCurrent");
		var moved = await CreateEnrolledStudentAsync(context, "EndedMembership", originalDivision);
		var moveDate = context.ReferenceDate.AddDays(-27);
		var queryDate = moveDate.AddDays(1);
		await EnsureWorkingDateAsync(context, queryDate);
		await MoveStudentToDivisionAsync(moved.StudentId, currentDivision, moveDate);
		var originalScopeUser = Guid.NewGuid();
		await SeedDivisionScopeAsync(originalScopeUser, originalDivision);
		using var originalClient = CreateClientAs(originalScopeUser);
		var response = await originalClient.GetAsync($"{AttendanceBaseUrl}/register?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={originalDivision}&startDate={queryDate:yyyy-MM-dd}&endDate={queryDate:yyyy-MM-dd}&maxResultCount=20");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var register = (await response.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!;
		register.TotalCount.ShouldBe(0); register.Rows.ShouldBeEmpty();
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Missing_roster_revision_rejects_save_without_creating_or_mutating_attendance()
	{
		var context = await ResolveSeededDivisionContextAsync();
		var division = await CreateDivisionAsync(context.StandardId, "MissingRosterRevision");
		var student = await CreateEnrolledStudentAsync(context, "MissingRosterRevision", division);
		var date = context.ReferenceDate.AddDays(-24);
		await EnsureWorkingDateAsync(context, date);
		var before = await ReadRosterAsync(context, division, date);
		foreach (var missingRevision in new string?[] { null, "" })
		{
			var input = Input(context, division, date, before, student.StudentId, AttendanceStatus.Present);
			input.RosterRevision = missingRevision;
			var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions);
			response.IsSuccessStatusCode.ShouldBeFalse();
			(await response.Content.ReadAsStringAsync()).ShouldContain(AttendanceOperationErrorCodes.RosterRevisionRequired);
			var after = await ReadRosterAsync(context, division, date);
			after.AttendanceDayId.ShouldBeNull(); after.Revision.ShouldBe(before.Revision);
			after.IsSubmitted.ShouldBeFalse();
			after.Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Pending);
		}
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Lock_and_unlock_advance_revision_and_reject_a_save_from_the_previous_revision()
	{
		var context = await ResolveSeededDivisionContextAsync();
		var division = await CreateDivisionAsync(context.StandardId, "LockRevision");
		var student = await CreateEnrolledStudentAsync(context, "LockRevision", division);
		var date = context.ReferenceDate.AddDays(-25);
		await SubmitAllAsync(context, division, date);
		var before = await ReadRosterAsync(context, division, date);
		var stale = Input(context, division, date, before, student.StudentId, AttendanceStatus.Absent);
		var command = new LockAttendanceDayInput { AcademicYearId = context.AcademicYearId,
			StandardId = context.StandardId, DivisionId = division, Date = date };
		var lockedResponse = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/lock", command, JsonOptions);
		lockedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockedResponse.Content.ReadAsStringAsync());
		var locked = await ReadRosterAsync(context, division, date);
		locked.IsLocked.ShouldBeTrue(); long.Parse(locked.Revision).ShouldBeGreaterThan(long.Parse(before.Revision));
		var unlockedResponse = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/unlock", command, JsonOptions);
		unlockedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await unlockedResponse.Content.ReadAsStringAsync());
		var unlocked = await ReadRosterAsync(context, division, date);
		unlocked.IsLocked.ShouldBeFalse(); long.Parse(unlocked.Revision).ShouldBeGreaterThan(long.Parse(locked.Revision));
		var staleResponse = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", stale, JsonOptions);
		staleResponse.IsSuccessStatusCode.ShouldBeFalse();
		(await staleResponse.Content.ReadAsStringAsync()).ShouldContain(AttendanceOperationErrorCodes.ConcurrencyConflict);
		var after = await ReadRosterAsync(context, division, date);
		after.Revision.ShouldBe(unlocked.Revision); after.IsSubmitted.ShouldBeTrue();
		after.Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Present);
	}

	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Register_uses_fixed_attendance_credit_and_threshold()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "FixedThreshold");
		var date = context.ReferenceDate;
		var student = await CreateEnrolledStudentAsync(context, "ThresholdHalfDay", division, attendanceStartDate: date);
		await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date);
		roster.Policy.HalfDayCredit.ShouldBe(.5m);
		roster.Policy.LowAttendanceThreshold.ShouldBe(75m);
		var input = Input(context, division, date, roster, student.StudentId, AttendanceStatus.Present);
		input.Entries[0].HalfDay = true; input.Submit = true;
		var mark = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions);
		mark.StatusCode.ShouldBe(HttpStatusCode.OK, await mark.Content.ReadAsStringAsync());
		var response = await Client.GetAsync($"{AttendanceBaseUrl}/register?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={division}&startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var row = (await response.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!.Rows.Single();
		row.StudentId.ShouldBe(student.StudentId); row.Percentage.ShouldBe(50d);
		row.LowAttendanceThreshold.ShouldBe(75m); row.IsBelowThreshold.ShouldBeTrue();
		row.ExpectedDays.ShouldBe(1); row.RecordedDays.ShouldBe(1); row.MissingDays.ShouldBe(0); row.IsProvisional.ShouldBeFalse();
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Register_includes_unmarked_historical_membership_after_division_move_within_authorized_scope()
	{
		var context = await ResolveSeededDivisionContextAsync();
		var originalDivision = await CreateDivisionAsync(context.StandardId, "UnmarkedHistorical");
		var currentDivision = await CreateDivisionAsync(context.StandardId, "UnmarkedCurrent");
		var moved = await CreateEnrolledStudentAsync(context, "UnmarkedMoved", originalDivision);
		var unrelated = await CreateEnrolledStudentAsync(context, "UnmarkedOutsideScope", currentDivision);
		var historicalDate = context.ReferenceDate.AddDays(-22);
		await EnsureWorkingDateAsync(context, historicalDate);
		await MoveStudentToDivisionAsync(moved.StudentId, currentDivision);
		var originalScopeUser = Guid.NewGuid();
		await SeedDivisionScopeAsync(originalScopeUser, originalDivision);
		using var originalClient = CreateClientAs(originalScopeUser);
		var url = $"{AttendanceBaseUrl}/register?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={originalDivision}&startDate={historicalDate:yyyy-MM-dd}&endDate={historicalDate:yyyy-MM-dd}&maxResultCount=20";
		var response = await originalClient.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var register = (await response.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!;
		register.TotalCount.ShouldBe(1);
		var row = register.Rows.Single();
		row.StudentId.ShouldBe(moved.StudentId);
		row.AcademicYearId.ShouldBe(context.AcademicYearId);
		row.ExpectedDays.ShouldBe(1); row.RecordedDays.ShouldBe(0); row.MissingDays.ShouldBe(1);
		row.Percentage.ShouldBeNull(); row.IsProvisional.ShouldBeTrue();
		register.Rows.ShouldNotContain(r => r.StudentId == unrelated.StudentId);
		var currentScopeUser = Guid.NewGuid();
		await SeedDivisionScopeAsync(currentScopeUser, currentDivision);
		using var currentClient = CreateClientAs(currentScopeUser);
		var denied = await currentClient.GetAsync(url);
		denied.StatusCode.ShouldBe(HttpStatusCode.OK, await denied.Content.ReadAsStringAsync());
		var outsideScope = (await denied.Content.ReadFromJsonAsync<AttendanceRegisterDto>(JsonOptions))!;
		outsideScope.TotalCount.ShouldBe(0); outsideScope.Rows.ShouldBeEmpty();
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Partial_draft_is_allowed_but_submit_checks_entire_eligible_roster()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "DraftComplete");
		var a = await CreateEnrolledStudentAsync(context, "DraftA", division); var b = await CreateEnrolledStudentAsync(context, "DraftB", division);
		var date = context.ReferenceDate.AddDays(-15); await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date);
		var input = Input(context, division, date, roster, a.StudentId, AttendanceStatus.Present);
		var saved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		roster = await ReadRosterAsync(context, division, date); roster.Students.Single(s => s.StudentId == b.StudentId).Status.ShouldBe(AttendanceStatus.Pending);
		input = Input(context, division, date, roster, a.StudentId, AttendanceStatus.Present); input.Submit = true;
		var incomplete = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions);
		incomplete.IsSuccessStatusCode.ShouldBeFalse();
		(await incomplete.Content.ReadAsStringAsync()).ShouldContain("IncompleteRoster");
		(await ReadRosterAsync(context, division, date)).IsSubmitted.ShouldBeFalse();
		await SubmitAllAsync(context, division, date); (await ReadRosterAsync(context, division, date)).IsSubmitted.ShouldBeTrue();
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Duplicate_and_invalid_enum_batches_do_not_partially_write()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "AtomicInvalid");
		var student = await CreateEnrolledStudentAsync(context, "AtomicInvalid", division); var date = context.ReferenceDate.AddDays(-16);
		await EnsureWorkingDateAsync(context, date); var roster = await ReadRosterAsync(context, division, date);
		var duplicate = Input(context, division, date, roster, student.StudentId, AttendanceStatus.Present);
		duplicate.Entries.Add(new() { StudentId = student.StudentId, Status = AttendanceStatus.Absent });
		var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", duplicate, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await ReadRosterAsync(context, division, date)).AttendanceDayId.ShouldBeNull();
		var invalid = Input(context, division, date, roster, student.StudentId, (AttendanceStatus)999);
		response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", invalid, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await ReadRosterAsync(context, division, date)).AttendanceDayId.ShouldBeNull();
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Identical_retry_returns_saved_result_and_stale_revision_rejects_overwrite()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "RetryConflict");
		var student = await CreateEnrolledStudentAsync(context, "RetryConflict", division); var date = context.ReferenceDate.AddDays(-17);
		await EnsureWorkingDateAsync(context, date); var roster = await ReadRosterAsync(context, division, date);
		var input = Input(context, division, date, roster, student.StudentId, AttendanceStatus.Present);
		var first = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions); first.StatusCode.ShouldBe(HttpStatusCode.OK);
		var original = await first.Content.ReadFromJsonAsync<MarkAttendanceOutput>(JsonOptions);
		var retry = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions); retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
		var repeated = await retry.Content.ReadFromJsonAsync<MarkAttendanceOutput>(JsonOptions); repeated!.Revision.ShouldBe(original!.Revision);
		input.OperationId = Guid.NewGuid(); input.Entries[0].Status = AttendanceStatus.Absent;
		var stale = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions); stale.IsSuccessStatusCode.ShouldBeFalse();
		(await stale.Content.ReadAsStringAsync()).ShouldContain("ConcurrencyConflict");
		(await ReadRosterAsync(context, division, date)).Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Present);
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Removed_individual_schedule_does_not_override_calendar()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "IndividualDay");
		var student = await CreateEnrolledStudentAsync(context, "IndividualDay", division); var date = context.ReferenceDate.AddDays(-18);
		await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date); var row = roster.Students.Single(s => s.StudentId == student.StudentId);
		row.IsExpected.ShouldBeTrue(); row.Status.ShouldBe(AttendanceStatus.Pending);
		var saved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/schedules", new { }, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Past_day_correction_requires_review_and_requester_cannot_self_approve()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "PastReview");
		var student = await CreateEnrolledStudentAsync(context, "PastReview", division); var date = context.ReferenceDate.AddDays(-19);
		await EnsureWorkingDateAsync(context, date); await SubmitAllAsync(context, division, date);
		var roster = await ReadRosterAsync(context, division, date);
		var request = new AttendanceCorrectionInput { AcademicYearId = context.AcademicYearId, StandardId = context.StandardId,
			DivisionId = division, Date = date, StudentId = student.StudentId, Status = AttendanceStatus.Absent, Reason = "Register observation corrected after review",
			Revision = roster.Revision, OperationId = Guid.NewGuid() };
		var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/corrections", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); var correction = await response.Content.ReadFromJsonAsync<AttendanceCorrectionDto>(JsonOptions);
		correction!.Status.ShouldBe("Pending");
		(await ReadRosterAsync(context, division, date)).Students.Single(s => s.StudentId == student.StudentId).Status.ShouldBe(AttendanceStatus.Present);
		var approve = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/corrections/{correction.Id}/approve", new AttendanceCorrectionDecisionInput { Revision = correction.Revision, Reason = "Self review attempt" }, JsonOptions);
		approve.IsSuccessStatusCode.ShouldBeFalse(); (await approve.Content.ReadAsStringAsync()).ShouldContain("SelfApproval");
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Current_day_active_enrollment_can_be_marked_before_historical_start_is_confirmed()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "CurrentUnconfirmedStart");
		var student = await CreateEnrolledStudentAsync(context, "CurrentUnconfirmedStart", division, confirmStart: false);
		var date = context.ReferenceDate;
		await EnsureWorkingDateAsync(context, date);
		var roster = await ReadRosterAsync(context, division, date);
		roster.IsExpected.ShouldBeTrue();
		roster.CanMark.ShouldBeTrue();
		roster.Students.Single(s => s.StudentId == student.StudentId).IsExpected.ShouldBeTrue();
		var save = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark",
			Input(context, division, date, roster, student.StudentId, AttendanceStatus.Present), JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		(await ReadRosterAsync(context, division, date)).Students.Single(s => s.StudentId == student.StudentId)
			.Status.ShouldBe(AttendanceStatus.Present);
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Unconfirmed_start_remains_unmarked_and_cannot_be_counted_as_an_absence()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "UnconfirmedStart");
		var student = await CreateEnrolledStudentAsync(context, "UnconfirmedStart", division, confirmStart: false); var date = context.ReferenceDate.AddDays(-20);
		await EnsureWorkingDateAsync(context, date); var roster = await ReadRosterAsync(context, division, date);
		var row = roster.Students.Single(s => s.StudentId == student.StudentId); row.IsExpected.ShouldBeFalse(); row.Status.ShouldBe(AttendanceStatus.Pending);
		var save = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", Input(context, division, date, roster, student.StudentId, AttendanceStatus.Absent), JsonOptions);
		save.IsSuccessStatusCode.ShouldBeFalse(); (await ReadRosterAsync(context, division, date)).AttendanceDayId.ShouldBeNull();
	}
	[Fact, TestTier(TestTraits.Tiers.Important)]
	public async Task Scoped_emergency_closure_blocks_daily_marking_and_marks_no_absences()
	{
		var context = await ResolveSeededDivisionContextAsync(); var division = await CreateDivisionAsync(context.StandardId, "EmergencyClosure");
		var student = await CreateEnrolledStudentAsync(context, "EmergencyClosure", division); var date = context.ReferenceDate.AddDays(-21);
		await EnsureWorkingDateAsync(context, date);
		using (var serviceScope = Factory.Services.CreateScope())
		using (serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var calendar = (await serviceScope.ServiceProvider.GetRequiredService<IRepository<AcademicCalendar, Guid>>().GetListAsync(c => c.AcademicMediumId == context.AcademicMediumId && c.AcademicYearId == context.AcademicYearId && c.Status == CalendarStatus.Published)).First();
			await serviceScope.ServiceProvider.GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), calendar.Id,
				date, DayType.EmergencyClosure, "Weather closure", appliesToAll: false, standardId: context.StandardId, divisionId: division)); await uow.CompleteAsync();
		}
		var roster = await ReadRosterAsync(context, division, date); roster.IsExpected.ShouldBeFalse(); roster.NotExpectedReason.ShouldBe("Weather closure");
		var save = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", Input(context, division, date, roster, student.StudentId, AttendanceStatus.Absent), JsonOptions);
		save.IsSuccessStatusCode.ShouldBeFalse(); (await ReadRosterAsync(context, division, date)).AttendanceDayId.ShouldBeNull();
	}
	private MarkAttendanceInput Input(SeededDivisionContext context, Guid division, DateTime date, StudentAttendanceListDto roster, Guid student, AttendanceStatus status) => new()
	{
		AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = division, Date = date,
		Revision = roster.Revision, RosterRevision = roster.RosterRevision, OperationId = Guid.NewGuid(), Entries = [new() { StudentId = student, Status = status }]
	};
	private async Task<StudentAttendanceListDto> ReadRosterAsync(SeededDivisionContext context, Guid division, DateTime date)
	{
		var response = await Client.GetAsync($"{AttendanceBaseUrl}/students?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={division}&date={date:yyyy-MM-dd}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions))!;
	}
	private async Task SubmitAllAsync(SeededDivisionContext context, Guid division, DateTime date)
	{
		await EnsureWorkingDateAsync(context, date); var roster = await ReadRosterAsync(context, division, date);
		var input = new MarkAttendanceInput { AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = division,
			Date = date, Revision = roster.Revision, RosterRevision = roster.RosterRevision, OperationId = Guid.NewGuid(), Submit = true,
			Entries = roster.Students.Where(s => s.IsExpected).Select(s => new AttendanceEntryDto { StudentId = s.StudentId, EnrollmentId = s.EnrollmentId,
				Status = s.Status == AttendanceStatus.Pending ? s.ApprovedLeaveId.HasValue ? AttendanceStatus.Absent : AttendanceStatus.Present : s.Status,
				Late = s.Late, HalfDay = s.HalfDay, Remarks = s.Remarks }).ToList() };
		var response = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", input, JsonOptions); response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}
	private async Task EnsureWorkingDateAsync(SeededDivisionContext context, DateTime date)
	{
		using var serviceScope = Factory.Services.CreateScope(); var services = serviceScope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var calendarRepo = services.GetRequiredService<IRepository<AcademicCalendar, Guid>>(); var dayRepo = services.GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(context.AcademicYearId);
			var calendar = (await calendarRepo.GetListAsync(c => c.AcademicMediumId == context.AcademicMediumId && c.AcademicYearId == context.AcademicYearId && c.Status == CalendarStatus.Published)).FirstOrDefault();
			if (calendar == null)
			{
				calendar = new(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), context.AcademicMediumId, context.AcademicYearId,
					"Attendance API regression calendar", year.StartDate, year.EndDate, isDefault: true); calendar.Publish(Guid.NewGuid()); await calendarRepo.InsertAsync(calendar);
			}
			if (!await dayRepo.AnyAsync(d => d.AcademicCalendarId == calendar.Id && d.Date == date.Date && d.DayType == DayType.WorkingDay))
				await dayRepo.InsertAsync(new(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), calendar.Id, date, DayType.WorkingDay, "Attendance regression working day"));
			await uow.CompleteAsync();
		}
	}
}
