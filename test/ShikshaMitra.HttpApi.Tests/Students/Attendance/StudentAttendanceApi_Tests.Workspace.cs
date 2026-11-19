using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
	private string? _addedDefaultAdminRole;

	public async Task InitializeAsync()
	{
		// The generic API fixture deliberately has staff scope and an admin claim only.
		// Attendance administrator scenarios require actual configured database membership.
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var adminRole = services.GetRequiredService<IOptions<UserScopeOptions>>().Value.AdminRoles.First();
			var roleManager = services.GetRequiredService<IdentityRoleManager>();
			if (await roleManager.FindByNameAsync(adminRole) == null)
				(await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), adminRole, Guid.Parse(TestAuthHandler.TestTenantId)))).Succeeded.ShouldBeTrue();
			var users = services.GetRequiredService<IdentityUserManager>();
			var user = (await users.FindByIdAsync(TestAuthHandler.TestUserId))!;
			if (!await users.IsInRoleAsync(user, adminRole))
			{
				(await users.AddToRoleAsync(user, adminRole)).Succeeded.ShouldBeTrue();
				_addedDefaultAdminRole = adminRole;
			}
			await uow.CompleteAsync();
		}
	}

	public async Task DisposeAsync()
	{
		if (_addedDefaultAdminRole == null) return;
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var users = services.GetRequiredService<IdentityUserManager>();
			var user = (await users.FindByIdAsync(TestAuthHandler.TestUserId))!;
			(await users.RemoveFromRoleAsync(user, _addedDefaultAdminRole)).Succeeded.ShouldBeTrue();
			await uow.CompleteAsync();
		}
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Workspace_matches_active_directory_population_pages_before_enrichment_and_keeps_ineligible_students_visible()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "WorkspacePopulation");
		var suffix = $"Workspace{Guid.NewGuid():N}";
		var first = await CreateEnrolledStudentAsync(context, suffix + "A", division);
		var second = await CreateEnrolledStudentAsync(context, suffix + "B", division);
		var future = await CreateEnrolledStudentAsync(context, suffix + "C", division, attendanceStartDate: context.ReferenceDate.AddDays(2));
		var excluded = await CreateEnrolledStudentAsync(context, suffix + "D", division, directoryEligible: false);
		await EnsureWorkingDateAsync(context, context.ReferenceDate);
		var roster = await ReadRosterAsync(context, division, context.ReferenceDate);
		roster.Students.ShouldContain(s => s.StudentId == first.StudentId && s.IsExpected);
		roster.Students.ShouldContain(s => s.StudentId == future.StudentId && !s.IsExpected && !string.IsNullOrWhiteSpace(s.NotExpectedReason));
		roster.Students.ShouldNotContain(s => s.StudentId == excluded.StudentId);
		var mark = Input(context, division, context.ReferenceDate, roster, first.StudentId, AttendanceStatus.Absent);
		mark.Entries[0].Remarks = "Absent with inline details";
		var saved = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", mark, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		using var attendanceOnly = CreatePermissionClient(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View);
		var query = $"academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}&boardId={context.AcademicBoardId}&mediumId={context.AcademicMediumId}&search={suffix}&sorting=name%20asc";
		var response = await attendanceOnly.GetAsync($"{AttendanceBaseUrl}/workspace?{query}&skipCount=0&maxResultCount=2");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var page = (await response.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		page.TotalCount.ShouldBe(3); page.Items.Count.ShouldBe(2);
		page.Items.Select(s => s.StudentId).ShouldBe(new[] { first.StudentId, second.StudentId });
		page.Items[0].Status.ShouldBe(AttendanceStatus.Absent); page.Items[0].Remarks.ShouldBe("Absent with inline details");
		var next = await attendanceOnly.GetAsync($"{AttendanceBaseUrl}/workspace?{query}&skipCount=2&maxResultCount=2");
		var nextPage = (await next.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		nextPage.TotalCount.ShouldBe(3); nextPage.Items.Single().StudentId.ShouldBe(future.StudentId);
		nextPage.Items.Single().IsExpected.ShouldBeFalse();
		nextPage.Items.Single().NotExpectedReason.ShouldNotBeNullOrWhiteSpace();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var enrollments = scope.ServiceProvider.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var enrollment = await enrollments.GetAsync(e => e.StudentId == second.StudentId && e.AcademicYearId == context.AcademicYearId);
			enrollment.ChangePlacement(context.StandardId, division, 72841, context.ReferenceDate);
			await enrollments.UpdateAsync(enrollment); await uow.CompleteAsync();
		}
		var rollSearch = await attendanceOnly.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}&divisionId={division}&search=72841&maxResultCount=1");
		rollSearch.StatusCode.ShouldBe(HttpStatusCode.OK, await rollSearch.Content.ReadAsStringAsync());
		var rollPage = (await rollSearch.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		rollPage.TotalCount.ShouldBe(1); rollPage.Items.Single().StudentId.ShouldBe(second.StudentId);
		rollPage.Items.Single().RollNo.ShouldBe(72841);
	}

	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData(null, 721, "721")]
	[InlineData("GR-2026-007", 721, "GR-2026-007")]
	[InlineData(null, null, null)]
	public async Task Workspace_and_class_roster_preserve_text_and_legacy_GR_numbers(string? grNumber, int? legacyGrNo, string? expected)
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "WorkspaceGrNumber");
		var suffix = $"WorkspaceGr{Guid.NewGuid():N}";
		var student = await CreateEnrolledStudentAsync(context, suffix, division);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var students = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
			var current = await (await students.GetQueryableWithoutAutoIncludesAsync()).SingleAsync(s => s.Id == student.StudentId);
			if (grNumber != null) current.AssignGrNumber(grNumber, legacyGrNo);
			else current.AssignGrNo(legacyGrNo);
			await students.UpdateAsync(current);
			await uow.CompleteAsync();
		}
		await EnsureWorkingDateAsync(context, context.ReferenceDate);
		var roster = await ReadRosterAsync(context, division, context.ReferenceDate);
		var rosterStudent = roster.Students.Single(s => s.StudentId == student.StudentId);
		rosterStudent.GrNumber.ShouldBe(expected);
		rosterStudent.StandardId.ShouldBe(context.StandardId);
		rosterStudent.DivisionId.ShouldBe(division);
		var response = await Client.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}&divisionId={division}&search={suffix}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var page = (await response.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		page.Items.Single(s => s.StudentId == student.StudentId).GrNumber.ShouldBe(expected);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Directory_options_accept_compatible_readers_and_active_certificate_selection_keeps_its_exact_permission()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		const string directory = "/api/students/records/directory";
		foreach (var permission in new[] {
			ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View,
			ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View,
			ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View })
		{
			using var client = CreatePermissionClient(permission);
			var options = await client.GetAsync($"{directory}/options?academicYearId={context.AcademicYearId}");
			options.StatusCode.ShouldBe(HttpStatusCode.OK, await options.Content.ReadAsStringAsync());
			var active = await client.GetAsync($"{directory}?includeActiveStudentList=true&population=Active&academicYearId={context.AcademicYearId}");
			active.StatusCode.ShouldBe(permission == ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View
				? HttpStatusCode.OK : HttpStatusCode.Forbidden, await active.Content.ReadAsStringAsync());
		}
		using var denied = CreatePermissionClient(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View);
		(await denied.GetAsync($"{directory}/options?academicYearId={context.AcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Teacher_exact_assignment_overrides_broader_scope_and_admin_claims_and_revocation_is_immediate()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var assigned = await CreateDivisionAsync(context.StandardId, "WorkspaceTeacher");
		var other = await CreateDivisionAsync(context.StandardId, "WorkspaceOther");
		var first = await CreateEnrolledStudentAsync(context, "AssignedTeacherStudent", assigned);
		var second = await CreateEnrolledStudentAsync(context, "OtherTeacherStudent", other);
		var teacher = Guid.NewGuid();
		await SeedDivisionScopeAsync(teacher, assigned, context.AcademicYearId);
		await SeedDivisionScopeAsync(teacher, other, context.AcademicYearId);
		var assignmentId = await AddTeacherAssignmentAsync(context, teacher, assigned);
		await EnsureWorkingDateAsync(context, context.ReferenceDate);
		using var teacherClient = CreateClientAs(teacher); // Default forged admin claim must not change database authority.
		var contextResponse = await teacherClient.GetAsync($"{AttendanceBaseUrl}/context?academicYearId={context.AcademicYearId}");
		contextResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await contextResponse.Content.ReadAsStringAsync());
		var options = (await contextResponse.Content.ReadFromJsonAsync<AttendanceWorkspaceContextDto>(JsonOptions))!;
		options.IsAdmin.ShouldBeFalse(); options.CanManageLock.ShouldBeTrue(); options.AssignedClasses.Count.ShouldBe(1);
		options.AssignedClasses.Single().DivisionId.ShouldBe(assigned);
		options.Options.Divisions.ShouldContain(d => d.Id == assigned); options.Options.Divisions.ShouldNotContain(d => d.Id == other);
		var assignedUrl = $"{AttendanceBaseUrl}/students?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={assigned}&date={context.ReferenceDate:yyyy-MM-dd}";
		var otherUrl = $"{AttendanceBaseUrl}/students?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={other}&date={context.ReferenceDate:yyyy-MM-dd}";
		var assignedRead = await teacherClient.GetAsync(assignedUrl);
		assignedRead.StatusCode.ShouldBe(HttpStatusCode.OK, await assignedRead.Content.ReadAsStringAsync());
		(await assignedRead.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions))!.Students.ShouldContain(s => s.StudentId == first.StudentId);
		var unassignedRead = await teacherClient.GetAsync(otherUrl);
		unassignedRead.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await unassignedRead.Content.ReadAsStringAsync()).ShouldNotContain(second.StudentId.ToString());
		var before = await ReadRosterAsync(context, assigned, context.ReferenceDate);
		var submission = Input(context, assigned, context.ReferenceDate, before, first.StudentId, AttendanceStatus.Present);
		submission.Submit = true;
		var allowed = await teacherClient.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", submission, JsonOptions);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
		var assignedHistory = await teacherClient.GetAsync($"{AttendanceBaseUrl}/history?studentId={first.StudentId}&startDate={context.ReferenceDate:yyyy-MM-dd}&endDate={context.ReferenceDate:yyyy-MM-dd}");
		assignedHistory.StatusCode.ShouldBe(HttpStatusCode.OK, await assignedHistory.Content.ReadAsStringAsync());
		(await assignedHistory.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions))!.TotalCount.ShouldBeGreaterThan(0);
		var forbiddenRoster = await ReadRosterAsync(context, other, context.ReferenceDate);
		var denied = await teacherClient.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", Input(context, other, context.ReferenceDate, forbiddenRoster, second.StudentId, AttendanceStatus.Present), JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var lockAllowed = await teacherClient.PostAsJsonAsync($"{AttendanceBaseUrl}/lock", new LockAttendanceDayInput {
			AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = assigned, Date = context.ReferenceDate }, JsonOptions);
		lockAllowed.StatusCode.ShouldBe(HttpStatusCode.OK, await lockAllowed.Content.ReadAsStringAsync());
		var unlockAllowed = await teacherClient.PostAsJsonAsync($"{AttendanceBaseUrl}/unlock", new LockAttendanceDayInput {
			AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = assigned, Date = context.ReferenceDate }, JsonOptions);
		unlockAllowed.StatusCode.ShouldBe(HttpStatusCode.OK, await unlockAllowed.Content.ReadAsStringAsync());
		var broad = await teacherClient.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}&maxResultCount=100");
		var broadRows = (await broad.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceDto>>(JsonOptions))!;
		broadRows.Items.ShouldContain(r => r.StudentId == first.StudentId); broadRows.Items.ShouldNotContain(r => r.StudentId == second.StudentId);
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var repository = services.GetRequiredService<IRepository<ClassTeacher, Guid>>();
			var assignment = await repository.GetAsync(assignmentId); assignment.MarkAsDraft(); await repository.UpdateAsync(assignment);
			await uow.CompleteAsync();
		}
		var latest = await ReadRosterAsync(context, assigned, context.ReferenceDate);
		var revoked = await teacherClient.PostAsJsonAsync($"{AttendanceBaseUrl}/mark", Input(context, assigned, context.ReferenceDate, latest, first.StudentId, AttendanceStatus.Absent), JsonOptions);
		revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await teacherClient.GetAsync(assignedUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var revokedHistory = await teacherClient.GetAsync($"{AttendanceBaseUrl}/history?studentId={first.StudentId}&startDate={context.ReferenceDate:yyyy-MM-dd}&endDate={context.ReferenceDate:yyyy-MM-dd}");
		revokedHistory.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await revokedHistory.Content.ReadFromJsonAsync<PagedResultDto<StudentAttendanceHistoryDto>>(JsonOptions))!.TotalCount.ShouldBe(0);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Workspace_requires_attendance_view_and_rejects_dates_outside_year_and_cross_tenant_reads()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		using var denied = CreatePermissionClient(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		(await denied.GetAsync($"{AttendanceBaseUrl}/context?academicYearId={context.AcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await denied.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var wrongDate = await Client.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.YearStartDate.AddDays(-1):yyyy-MM-dd}");
		wrongDate.IsSuccessStatusCode.ShouldBeFalse(); (await wrongDate.Content.ReadAsStringAsync()).ShouldContain(AttendanceOperationErrorCodes.DateOutsideAcademicYear);
		using var crossTenant = Factory.CreateClient(); crossTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await crossTenant.GetAsync($"{AttendanceBaseUrl}/context?academicYearId={context.AcademicYearId}")).IsSuccessStatusCode.ShouldBeFalse();
		(await crossTenant.GetAsync($"{AttendanceBaseUrl}/workspace?academicYearId={context.AcademicYearId}&date={context.ReferenceDate:yyyy-MM-dd}")).IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Admin_lock_rejects_changed_submitted_roster_until_full_roster_is_resubmitted()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "WorkspaceLockReview");
		var date = context.ReferenceDate.AddDays(-4);
		await CreateEnrolledStudentAsync(context, "WorkspaceLockA", division, attendanceStartDate: date);
		await EnsureWorkingDateAsync(context, date);
		await SubmitAllAsync(context, division, date);
		await CreateEnrolledStudentAsync(context, "WorkspaceLockB", division, attendanceStartDate: date);
		var lockInput = new LockAttendanceDayInput { AcademicYearId = context.AcademicYearId, StandardId = context.StandardId, DivisionId = division, Date = date };
		var blocked = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/lock", lockInput, JsonOptions);
		blocked.IsSuccessStatusCode.ShouldBeFalse();
		await SubmitAllAsync(context, division, date);
		var allowed = await Client.PostAsJsonAsync($"{AttendanceBaseUrl}/lock", lockInput, JsonOptions);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
	}

	private async Task<Guid> AddTeacherAssignmentAsync(SeededDivisionContext context, Guid userId, Guid divisionId)
	{
		using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (services.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var role = new IdentityRole(Guid.NewGuid(), $"AttendanceTeacher{Guid.NewGuid():N}", tenantId);
			role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
			role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
			await services.GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(role, autoSave: true);
			var identity = userId.ToByteArray();
			var pan = new string(identity.Take(5).Select(value => (char)('A' + value % 26)).ToArray()) +
				(BitConverter.ToUInt32(identity, 8) % 10000).ToString("D4") + "A";
			var aadhaar = "9" + (BitConverter.ToUInt64(identity, 0) % 100000000000).ToString("D11");
			var employee = await services.GetRequiredService<EmployeeManager>().CreateEmployeeAsync(userId, role.Id,
				"Attendance", "Teacher", null, "Attendance", EmployeeStatusType.Active, null,
				panNo: pan, adharNo: aadhaar, employeeGroup: EmployeeGroupType.Teaching);
			await services.GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, autoSave: true);
			var assignment = new ClassTeacher(Guid.NewGuid(), tenantId, context.StandardId, divisionId, employee.Id,
				context.AcademicMediumId, context.AcademicYearId, context.AcademicBoardId, context.AcademicMediumId, context.AcademicMediumStageId);
			await services.GetRequiredService<IRepository<ClassTeacher, Guid>>().InsertAsync(assignment);
			await uow.CompleteAsync(); return assignment.Id;
		}
	}
}
