using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.InitialSetup;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Identity;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InitialSetupApi_Tests
{
	[Fact]
	public async Task Legacy_creation_opts_in_only_new_tenant_and_initializes_its_already_seeded_year()
	{
		var hostId = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		using var tenantContext = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(hostId, "legacy-host-" + hostId.ToString("N"), "host@example.test"));
			await unit.CompleteAsync();
		}
		using var host = Factory.CreateClient();
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, hostId.ToString());
		var input = new TenantCreateDto { Name = "legacy" + Guid.NewGuid().ToString("N"), AdminEmailAddress = "admin@example.test", AdminPassword = "Synthetic-Legacy-29!" };
		foreach (var key in new[] { "InstituteName", "Email", "ContactNumber", "Address1", "City", "State", "Country", "PostalCode" })
			input.ExtraProperties[key] = key == "Email" ? "school@example.test" : "Synthetic";
		var response = await host.PostAsJsonAsync("/api/multi-tenancy/tenants", input);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var created = (await response.Content.ReadFromJsonAsync<TenantDto>())!;
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(created.Id))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			(await scope.ServiceProvider.GetRequiredService<ITenantRepository>().GetAsync(created.Id)).ConcurrencyStamp.ShouldBe(created.ConcurrencyStamp);
			var year = (await scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetListAsync()).Single();
			year.GetProperty(HrStarterPolicyDefaults.YearInitializedProperty, false).ShouldBeTrue();
			var resolved = await scope.ServiceProvider.GetRequiredService<HrPolicyResolver>().ResolvePreviewAsync(null, null, year.StartDate, year.Id);
			resolved.LeavePolicy.ShouldNotBeNull(); resolved.AttendancePolicy.IsDefault.ShouldBeTrue();
			(await scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>().GetListAsync()).Count.ShouldBe(1);
			await unit.CompleteAsync();
		}
	}

	[Fact]
	public async Task Fresh_school_gets_editable_policy_set_and_employee_finalization_prerequisites()
	{
		using var school = await NewSchoolAsync();
		var progress = await SaveHrYearAsync(school);
		var yearId = progress.AcademicYearId!.Value;
		var calendarPage = (await school.GetFromJsonAsync<PagedResultDto<WorkCalendarPolicyDto>>(
			$"/api/hr/work-calendar-policies?academicYearId={yearId}"))!;
		calendarPage.TotalCount.ShouldBe(1);
		var calendar = calendarPage.Items.Single();
		calendar.Name.ShouldContain("Starter"); calendar.Status.ShouldBe("active");
		calendar.WeeklyOffPattern.ShouldBe("saturday_sunday");
		calendar.HolidaySyncMode.ShouldBe("linked_with_overrides"); calendar.Holidays.ShouldBeEmpty();
		var payroll = (await school.GetFromJsonAsync<PagedResultDto<PayrollPolicyDto>>(
			$"/api/hr/payroll/policies?academicYearId={yearId}"))!.Items.Single();
		payroll.IsDefault.ShouldBeTrue(); payroll.WorkCalendarPolicyId.ShouldBe(calendar.Id);
		var attendance = (await school.GetFromJsonAsync<PagedResultDto<AttendancePolicyDto>>(
			$"/api/hr/attendance/policies?academicYearId={yearId}"))!.Items.Single();
		attendance.IsDefault.ShouldBeTrue(); attendance.WorkCalendarPolicyId.ShouldBe(calendar.Id);
		attendance.CaptureMode.ShouldBe("manual_status"); attendance.PenaltyType.ShouldBe("none");
		attendance.OvertimeEnabled.ShouldBeFalse(); attendance.PayrollPreviewEnabled.ShouldBeFalse();
		var leave = (await school.GetFromJsonAsync<LeavePolicyListResponse>(
			$"/api/leave-policies?academicYearId={yearId}"))!.Items.Single();
		leave.IsDefault.ShouldBeTrue(); leave.Description!.ShouldContain("unconfigured");
		leave.Rules.Count.ShouldBe(2);
		leave.Rules.ShouldAllBe(x => x.LeaveCount == 0 && x.RequiresApproval && !x.AllowNegativeBalance && !x.CarryForward && !x.EncashmentAllowed);
		await InHrTenantAsync(school, async services =>
		{
			// This is the same preview resolver used when finalization supplies default policy references.
			var resolved = await services.GetRequiredService<HrPolicyResolver>().ResolvePreviewAsync(null, null, new DateTime(2026, 7, 1), yearId);
			resolved.PayrollPolicy.Id.ShouldBe(payroll.Id); resolved.AttendancePolicy.Id.ShouldBe(attendance.Id);
			resolved.LeavePolicy!.Id.ShouldBe(leave.Id);
			resolved.PayrollWorkCalendarPolicy.Id.ShouldBe(calendar.Id);
			resolved.AttendanceWorkCalendarPolicy.Id.ShouldBe(calendar.Id);
			var workingUnits = PayrollMonthCalendar.BuildWorkingUnitsByDate(2026, 6, [],
				resolved.PayrollPolicy.SaturdayWorkPolicy, [], resolved.PayrollWorkCalendarPolicy);
			workingUnits[new DateOnly(2026, 6, 1)].ShouldBe(1m);
			workingUnits[new DateOnly(2026, 6, 6)].ShouldBe(0m);
			workingUnits[new DateOnly(2026, 6, 7)].ShouldBe(0m);
		});

		var calendarEdit = new CreateUpdateWorkCalendarPolicyDto {
			AcademicYearId = yearId, Name = "Our editable school calendar", Status = "active",
			EffectiveFrom = calendar.EffectiveFrom, EffectiveTo = calendar.EffectiveTo,
			WeeklyOffPattern = "sunday", HolidaySyncMode = calendar.HolidaySyncMode };
		// Referencing active defaults require the ordinary impact-preview confirmation flow.
		(await school.PutAsJsonAsync($"/api/hr/work-calendar-policies/{calendar.Id}", calendarEdit)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var previewResponse = await school.PostAsJsonAsync("/api/hr/policies/impact-preview", new HrPolicyImpactPreviewRequestDto {
			PolicyType = "work_calendar", PolicyId = calendar.Id, AcademicYearId = yearId, Action = "update", WorkCalendarPolicy = calendarEdit });
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<HrPolicyImpactPreviewDto>())!;
		using var editRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/hr/work-calendar-policies/{calendar.Id}") { Content = JsonContent.Create(calendarEdit) };
		editRequest.Headers.Add("X-HR-Policy-Impact-Token", preview.ConfirmationToken!);
		var edited = await school.SendAsync(editRequest);
		edited.StatusCode.ShouldBe(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
		(await school.GetFromJsonAsync<WorkCalendarPolicyDto>($"/api/hr/work-calendar-policies/{calendar.Id}"))!.WeeklyOffPattern.ShouldBe("sunday");
		var leaveEdit = await school.PutAsJsonAsync($"/api/leave-policies/{leave.Id}", new LeavePolicyUpdateRequest {
			PolicyTitle = "Our reviewed synthetic leave policy", Status = "Active", IsDefault = true,
			EffectiveFrom = leave.EffectiveFrom, EffectiveTo = leave.EffectiveTo, Description = "Synthetic administrator choice",
			Rules = leave.Rules.Select(rule => new LeavePolicyRuleRequest {
				PaidType = rule.PaidType, EntitlementMode = rule.EntitlementMode, AccrualPolicyType = rule.AccrualPolicyType,
				LeaveCount = 7, RequiresApproval = true, MaxDaysPerRequest = 7, AllowHalfDay = true, IsActive = true }).ToList() });
		leaveEdit.StatusCode.ShouldBe(HttpStatusCode.OK, await leaveEdit.Content.ReadAsStringAsync());
		await InHrTenantAsync(school, async services => await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(yearId)));
		(await school.GetFromJsonAsync<LeavePolicyDetailResponse>($"/api/leave-policies/{leave.Id}"))!.Rules.ShouldAllBe(rule => rule.LeaveCount == 7);
		var shift = (await school.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>("/api/app/shift-management"))!.Items.Single();
		shift.StartTime.ShouldBe(new TimeOnly(7, 0)); shift.EndTime.ShouldBe(new TimeOnly(12, 30));
		await InHrTenantAsync(school, async services =>
		{
			await services.GetRequiredService<ShiftReferenceValidator>().EnsureExistsAsync(shift.Id);
			var role = await services.GetRequiredService<IIdentityRoleRepository>().FindByNormalizedNameAsync("TEACHER");
			role.ShouldNotBeNull();
			var employeeInput = new CreateEmployeePersonalInfoDto {
				FirstName = "Synthetic", LastName = "Starter", MobileNo = "9000000001", Email = "starter@example.test",
				PanNo = "ABCDE1234F", AdharNo = "123456789012", CastType = CastType.General, FamilyCaste = "Synthetic",
				EmployeeGroup = EmployeeGroupType.Teaching, RoleId = role.Id, ShiftId = shift.Id, DateOfJoining = new DateOnly(2026, 7, 1),
				PolicyOverride = new CreateEmployeePolicyOverrideDto { AcademicYearId = yearId,
					AttendancePolicyId = attendance.Id, PayrollPolicyId = payroll.Id, LeavePolicyId = leave.Id, EffectiveFrom = new DateTime(2026, 7, 1) } };
			var validation = await new CreateEmployeePersonalInfoDtoValidator(services.GetRequiredService<IClock>()).ValidateAsync(employeeInput);
			validation.Errors.ShouldBeEmpty();
		});
		var shiftEdit = await school.PutAsJsonAsync($"/api/app/shift-management/{shift.Id}", new CreateUpdateShiftManagementDto {
			ShiftName = "Reviewed school shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0) });
		shiftEdit.StatusCode.ShouldBe(HttpStatusCode.OK, await shiftEdit.Content.ReadAsStringAsync());
		await InHrTenantAsync(school, async services =>
		{
			var nextYear = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Next year", new DateTime(2027, 6, 1), new DateTime(2028, 5, 31));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(nextYear, autoSave: true);
		});
		var retainedShift = (await school.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>("/api/app/shift-management"))!.Items.Single();
		retainedShift.Id.ShouldBe(shift.Id); retainedShift.ShiftName.ShouldBe("Reviewed school shift");
		retainedShift.StartTime.ShouldBe(new TimeOnly(8, 0));
	}

	[Fact]
	public async Task Retry_preserves_customization_and_does_not_recreate_removed_records()
	{
		using var school = await NewSchoolAsync();
		var yearId = (await SaveHrYearAsync(school)).AcademicYearId!.Value;
		Guid calendarId = Guid.Empty;
		await InHrTenantAsync(school, async services =>
		{
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(yearId);
			year.GetProperty(HrStarterPolicyDefaults.YearInitializedProperty, false).ShouldBeTrue();
			var calendars = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
			var calendar = (await calendars.GetListAsync()).Single(); calendarId = calendar.Id;
			calendar.Update("Customized", HrPolicyStatus.Active, year.StartDate, year.EndDate,
				AttendanceWeeklyOffPattern.Sunday, [], WorkCalendarHolidaySyncMode.Independent);
			await calendars.UpdateAsync(calendar);
			await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(year);
			await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(year);
		});
		await InHrTenantAsync(school, async services =>
		{
			(await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetAsync(calendarId)).Name.ShouldBe("Customized");
			(await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().GetListAsync()).Count.ShouldBe(1);
			(await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().GetListAsync()).Count.ShouldBe(1);
			var leaves = services.GetRequiredService<IRepository<LeavePolicy, Guid>>();
			await leaves.DeleteAsync((await leaves.GetListAsync()).Single());
		});
		await InHrTenantAsync(school, async services =>
		{
			await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(
				await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(yearId));
			(await services.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task New_schools_are_isolated_and_legacy_tenants_do_not_receive_a_backfill()
	{
		using var first = await NewSchoolAsync(); using var second = await NewSchoolAsync();
		var firstYear = (await SaveHrYearAsync(first)).AcademicYearId!.Value;
		var secondYear = (await SaveHrYearAsync(second)).AcademicYearId!.Value;
		Guid firstCalendarId = Guid.Empty;
		await InHrTenantAsync(first, async services => firstCalendarId = (await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).Single().Id);
		(await second.GetAsync($"/api/hr/work-calendar-policies/{firstCalendarId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		await InHrTenantAsync(second, async services =>
		{
			var calendars = await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync();
			calendars.Count.ShouldBe(1); calendars.Single().AcademicYearId.ShouldBe(secondYear);
			calendars.Single().AcademicYearId.ShouldNotBe(firstYear);
		});
		// Simulate a pre-feature school with no opt-in marker, using only synthetic local records.
		await InHrTenantAsync(first, async services =>
		{
			var tenants = services.GetRequiredService<ITenantRepository>();
			var tenant = await tenants.GetAsync(HrTenantId(first));
			tenant.SetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, false); await tenants.UpdateAsync(tenant);
			var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Legacy year", new DateTime(2028, 6, 1), new DateTime(2029, 5, 31));
			await years.InsertAsync(year, autoSave: true);
			(await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).Count.ShouldBe(1);
			year.GetProperty(HrStarterPolicyDefaults.YearInitializedProperty, false).ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Policy_failure_rolls_back_year_and_retry_can_create_complete_set()
	{
		using var school = await NewSchoolAsync();
		var failedId = Guid.Empty;
		await Should.ThrowAsync<BusinessException>(async () => await InHrTenantAsync(school, async services =>
		{
			// AcademicYear accepts these dates, but policy date bounds reject them during the local event.
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Unsupported starter year", new DateTime(2201, 1, 1), new DateTime(2202, 1, 1));
			failedId = year.Id;
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
		}));
		await InHrTenantAsync(school, async services =>
		{
			(await services.GetRequiredService<IRepository<AcademicYear, Guid>>().FindAsync(failedId)).ShouldBeNull();
			(await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
		});
		// Also fail after all policies/rules have reached the database, before the enclosing commit.
		await Should.ThrowAsync<InvalidOperationException>(async () => await InHrTenantAsync(school, async services =>
		{
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Rolled-back complete set", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
			// ABP dispatches queued local entity events during CompleteAsync. Invoke the same
			// initializer here to test a failure after its rows have been flushed, before completion.
			await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(year);
			await services.GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
			(await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().GetListAsync()).Count.ShouldBe(1);
			(await services.GetRequiredService<IRepository<LeavePolicyRule, Guid>>().GetListAsync()).Count.ShouldBe(2);
			throw new InvalidOperationException("Synthetic failure before commit");
		}));
		await InHrTenantAsync(school, async services =>
		{
			(await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<LeavePolicyRule, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().GetListAsync()).ShouldBeEmpty();
		});
		(await SaveHrYearAsync(school)).AcademicYearId.ShouldNotBeNull();
	}

	[Fact]
	public async Task Existing_partial_policy_setup_is_not_promoted_replaced_or_completed_silently()
	{
		using var school = await NewSchoolAsync();
		Guid yearId = Guid.Empty;
		await InHrTenantAsync(school, async services =>
		{
			var tenants = services.GetRequiredService<ITenantRepository>();
			var tenant = await tenants.GetAsync(HrTenantId(school));
			tenant.SetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, false); await tenants.UpdateAsync(tenant);
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Customized year", new DateTime(2026, 6, 1), new DateTime(2027, 5, 31));
			yearId = year.Id;
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, autoSave: true);
			await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().InsertAsync(new WorkCalendarPolicy(
				Guid.NewGuid(), year.TenantId, year.Id, "Administrator's draft calendar", HrPolicyStatus.Draft,
				null, null, AttendanceWeeklyOffPattern.Sunday, [], WorkCalendarHolidaySyncMode.Independent), autoSave: true);
			tenant.SetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, true); await tenants.UpdateAsync(tenant);
		});
		await InHrTenantAsync(school, async services =>
		{
			await services.GetRequiredService<HrStarterPolicyInitializer>().InitializeAsync(
				await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(yearId));
			var calendar = (await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().GetListAsync()).Single();
			calendar.Status.ShouldBe(HrPolicyStatus.Draft); calendar.Name.ShouldBe("Administrator's draft calendar");
			(await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
			(await services.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetListAsync()).ShouldBeEmpty();
		});
	}

	private async Task<InitialSetupDto> SaveHrYearAsync(HttpClient school)
	{
		var progress = await PrepareYearStepAsync(school);
		return await PutAsync(school, "academic-year", new SaveSetupYearInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, Name = "2026-2027",
			StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 5, 31) });
	}
	private static Guid HrTenantId(HttpClient school) => Guid.Parse(school.DefaultRequestHeaders.GetValues(TestAuthHandler.TestTenantIdHeader).Single());
	private async Task InHrTenantAsync(HttpClient school, Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(HrTenantId(school));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
}
