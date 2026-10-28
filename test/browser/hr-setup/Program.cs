using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Setup;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

await using var factory = new BrowserFactory();
using var client = factory.CreateClient();
Guid tenantId = default, actorId = default;
AcademicYear year = null!;
await SeedAsync();
using var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:4396/");
listener.Start();
Console.WriteLine("HR setup browser bridge ready: disposable SQLite and real ABP APIs with synthetic authentication.");
while (true)
{
	var context = await listener.GetContextAsync();
	try
	{
		if (context.Request.RawUrl == "/fixture/reset" && context.Request.HttpMethod == "POST") await SeedAsync();
		if (context.Request.RawUrl is "/fixture/context" or "/fixture/reset")
		{
			context.Response.ContentType = "application/json";
			await JsonSerializer.SerializeAsync(context.Response.OutputStream, new { tenantId, actorId, year = new { id = year.Id, name = year.Name, startDate = year.StartDate, endDate = year.EndDate } });
			context.Response.Close(); continue;
		}
		using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.RawUrl);
		if (context.Request.HasEntityBody)
		{
			using var reader = new StreamReader(context.Request.InputStream);
			request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, context.Request.ContentType?.Split(';')[0] ?? "application/json");
		}
		using var response = await client.SendAsync(request);
		context.Response.StatusCode = (int) response.StatusCode;
		context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
		await context.Response.OutputStream.WriteAsync(await response.Content.ReadAsByteArrayAsync());
		context.Response.Close();
	}
	catch (Exception exception)
	{
		Console.WriteLine(exception.Message);
		context.Response.StatusCode = 500; context.Response.Close();
	}
}

async Task SeedAsync()
{
	await InAsync(null, async services =>
	{
		var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"Browser setup {Guid.NewGuid():N}");
		await services.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); tenantId = tenant.Id;
	});
	await InAsync(tenantId, async services =>
	{
		var actor = new IdentityUser(Guid.NewGuid(), "setup-browser", "setup-browser@example.test", tenantId);
		await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true); actorId = actor.Id;
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: actor.Id, description: "Synthetic school-wide browser scope"), true);
		year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("2026-2027", new(2026, 4, 1), new(2027, 3, 31));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var role = new IdentityRole(Guid.NewGuid(), "Approved staff", tenantId);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
		await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
		// Leave the shift and payroll catalogue empty so the browser must save real configuration.
		var calendar = HrStarterPolicyDefaults.Calendar(Guid.NewGuid(), year);
		await services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().InsertAsync(calendar, true);
		await services.GetRequiredService<IRepository<PayrollPolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Payroll(Guid.NewGuid(), year, calendar.Id), true);
		await services.GetRequiredService<IRepository<AttendancePolicy, Guid>>().InsertAsync(HrStarterPolicyDefaults.Attendance(Guid.NewGuid(), year, calendar.Id), true);
		await services.GetRequiredService<ILeavePolicyRepository>().InsertAsync(HrStarterPolicyDefaults.Leave(Guid.NewGuid(), year, Guid.NewGuid(), Guid.NewGuid()), true);
	});
	client.DefaultRequestHeaders.Remove(TestAuthHandler.TestTenantIdHeader);
	client.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserIdHeader);
	client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
	client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actorId.ToString());
}

async Task InAsync(Guid? id, Func<IServiceProvider, Task> action)
{
	using var scope = factory.Services.CreateScope();
	using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(id);
	using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
	await action(scope.ServiceProvider); await unit.CompleteAsync();
}

class BrowserFactory : HrSetupReadinessTestFactory
{
	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseContentRoot(builder, Path.GetFullPath("src/ShikshaMitra.HttpApi.Host"));
	}
}