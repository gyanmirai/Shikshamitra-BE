using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.Data;
using ShikshaMitra.Common;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.WorkShifts;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR;

[Trait("Module", "HR"), Trait("Tier", "Critical")]
public class EmployeeWorkShiftApi_Tests(ShiftTenantTestFactory factory) : IClassFixture<ShiftTenantTestFactory>
{
	private const string Url = "/api/hr/work-shifts";
	private async Task<(HttpClient Client, Guid Tenant)> ClientAsync()
	{
		var id = Guid.NewGuid(); await factory.SeedTenantAsync(id, "work-shifts-" + id);
		var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, id.ToString()); return (client, id);
	}
	private static SaveWorkShiftInput Input(string name = "Office", string? stamp = null, int start = 9) => new() { ShiftName = name, StartTime = new(start, 0), EndTime = new(17, 0), ConcurrencyStamp = stamp };
	private static async Task<EmployeeWorkShiftDto> CreateAsync(HttpClient client)
	{ var r = await client.PostAsJsonAsync(Url, Input()); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<EmployeeWorkShiftDto>())!; }
	[Fact]
	public async Task Catalogues_are_independent_and_actions_are_not_interchangeable()
	{
		var (a, tenant) = await ClientAsync(); using var client = a; var (b, _) = await ClientAsync(); using var other = b;
		var shift = await CreateAsync(client);
		(await client.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>("/api/app/shift-management"))!.TotalCount.ShouldBe(0);
		var school = await client.PostAsJsonAsync("/api/app/shift-management", new { shiftName = "Morning school", startTime = "07:00:00", endTime = "12:00:00" }); school.EnsureSuccessStatusCode();
		var updated = await client.PutAsJsonAsync(Url + "/" + shift.Id, Input("Office updated", shift.ConcurrencyStamp, 10)); updated.EnsureSuccessStatusCode();
		var schoolRows = (await client.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>("/api/app/shift-management"))!;
		schoolRows.Items.Single().StartTime.ShouldBe(new TimeOnly(7, 0));
		(await other.PutAsJsonAsync(Url + "/" + shift.Id, Input())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await other.GetFromJsonAsync<PagedResultDto<EmployeeWorkShiftDto>>(Url))!.TotalCount.ShouldBe(0);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Employee.EmployeeList.Edit");
		(await client.GetAsync(Url + "/lookup")).IsSuccessStatusCode.ShouldBeTrue();
		(await client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync(Url, Input())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.PostAsJsonAsync("/api/app/shift-management", new { shiftName = "Denied", startTime = "07:00:00", endTime = "12:00:00" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Daytime_validation_and_stale_writes_are_enforced()
	{
		var (a, _) = await ClientAsync(); using var client = a;
		(await client.PostAsJsonAsync(Url, Input(start: 22))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var shift = await CreateAsync(client);
		(await client.PutAsJsonAsync(Url + "/" + shift.Id, Input("Wrong stamp", "stale"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
	[Fact]
	public async Task Scheduling_preserves_later_changes_and_prevents_history_mutation()
	{
		var (a, tenant) = await ClientAsync(); using var client = a;
		var shift = await CreateAsync(client); var employeeId = Guid.Empty; var actor = Guid.NewGuid(); DateOnly today = default;
		using (var scope = factory.Services.CreateScope())
		using (var t = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant))
		using (var u = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var services = scope.ServiceProvider;
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(actor, "operator", "operator@example.test", tenant), true);
			var role = new IdentityRole(Guid.NewGuid(), "Staff", tenant); role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee); role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true); await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenant, userId: actor), true);
			today = await services.GetRequiredService<EmployeeShiftResolver>().TodayAsync();
			var employee = await services.GetRequiredService<EmployeeManager>().CreateEmployeeAsync(actor, role.Id, "Test", "Employee", null, "TE", EmployeeStatusType.Active, null, panNo: "ABCDE1234F", adharNo: "123412341234", dateOfJoining: today.AddDays(-100), employeeGroup: EmployeeGroupType.NonTeaching);
			employeeId = employee.Id; await services.GetRequiredService<IEmployeeRepository>().InsertAsync(employee, true); await u.CompleteAsync();
		}
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actor.ToString());
		var url = $"/api/hr/employees/{employeeId}/shift-assignments";
		async Task<EmployeeWorkScheduleDto> Read() => (await client.GetFromJsonAsync<EmployeeWorkScheduleDto>(url))!;
		async Task<EmployeeWorkScheduleDto> Schedule(DateOnly date)
		{ var current = await Read(); var response = await client.PostAsJsonAsync(url, new ScheduleEmployeeShiftInput { WorkShiftId = shift.Id, EffectiveFrom = date, ConcurrencyStamp = current.ConcurrencyStamp }); response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<EmployeeWorkScheduleDto>())!; }
		await Schedule(today); await Schedule(today.AddDays(20)); var schedule = await Schedule(today.AddDays(10));
		schedule.Upcoming.Count.ShouldBe(2); schedule.Current!.EffectiveTo.ShouldBe(today.AddDays(10));
		var cancelled = await client.DeleteAsync(url + "/" + schedule.Upcoming.First().Id + "?concurrencyStamp=" + schedule.ConcurrencyStamp); cancelled.EnsureSuccessStatusCode();
		schedule = await Read(); schedule.Current!.EffectiveTo.ShouldBe(today.AddDays(20)); schedule.Upcoming.Count.ShouldBe(1);
		(await client.PostAsJsonAsync(url, new ScheduleEmployeeShiftInput { WorkShiftId = shift.Id, EffectiveFrom = today.AddDays(-1), ConcurrencyStamp = schedule.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		// A request cannot reuse an old schedule token after a successful change.
		(await client.PostAsJsonAsync(url, new ScheduleEmployeeShiftInput { WorkShiftId = shift.Id, EffectiveFrom = today.AddDays(3), ConcurrencyStamp = "stale" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		using (var scope = factory.Services.CreateScope())
		using (var t = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant))
		using (var u = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var attendance = new EmployeeAttendance { TenantId = tenant, EmployeeId = employeeId, AttendanceDate = today };
			typeof(EmployeeAttendance).GetProperty(nameof(EmployeeAttendance.Id))!.SetValue(attendance, Guid.NewGuid());
			await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>().InsertAsync(attendance, true);
			var lockedMonth = new PayrollRunItemSnapshot(Guid.NewGuid(), tenant, today.AddMonths(1).ToString("yyyy-MM"), employeeId) { IsLocked = true };
			await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(lockedMonth, true);
			await u.CompleteAsync();
		}
	 (await client.PostAsJsonAsync(url, new ScheduleEmployeeShiftInput { WorkShiftId = shift.Id, EffectiveFrom = today, ConcurrencyStamp = schedule.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.PostAsJsonAsync(url, new ScheduleEmployeeShiftInput { WorkShiftId = shift.Id, EffectiveFrom = today.AddMonths(1), ConcurrencyStamp = schedule.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var catalog = (await client.GetFromJsonAsync<PagedResultDto<EmployeeWorkShiftDto>>(Url))!.Items.Single();
		(await client.PutAsJsonAsync(Url + "/" + shift.Id, Input("Changed timing", catalog.ConcurrencyStamp, 10))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.DeleteAsync(Url + "/" + shift.Id + "?concurrencyStamp=" + catalog.ConcurrencyStamp)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
}