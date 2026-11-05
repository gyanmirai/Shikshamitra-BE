using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.WorkShifts;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using System.Net;
using System.Net.Http.Json;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.Infrastructure.Auth;

await using var factory = new BrowserFactory();
var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
await factory.SeedTenantAsync(a, "Synthetic DDVS");
await factory.SeedTenantAsync(b, "Synthetic Swaminarayan");
using var client = factory.CreateClient();
foreach (var (tenant, names) in new[] {
	(a.ToString(), new[] { "Mayur shift", "Full Day Shift", "Afternoon Shift", "Morning Shift" }),
	("host", new[] { "Legacy unassigned shift" }) })
{
	foreach (var name in names)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/app/shift-management");
		request.Headers.Add(TestAuthHandler.TestTenantIdHeader, tenant);
		request.Content = JsonContent.Create(new { shiftName = name, startTime = "08:00:00", endTime = "16:00:00" });
		using var response = await client.SendAsync(request);
		response.EnsureSuccessStatusCode();
	}
}
Guid employeeId; DateOnly today;
using(var scope=factory.Services.CreateScope())
using(var tenant=scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(a))
using(var unit=scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew:true,isTransactional:true))
{
 var services=scope.ServiceProvider; var actor=Guid.Parse(TestAuthHandler.TestUserId);
 await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(actor,"shift-browser","shift-browser@example.test",a),true);
 var role=new IdentityRole(Guid.NewGuid(),"Browser Staff",a);role.SetProperty(EmployeeRoleAudience.ExtraPropertyName,EmployeeRoleAudience.Employee);role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName,true);
 await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role,true);
 await services.GetRequiredService<IRepository<UserScopeAssignment,Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),a,userId:actor),true);
 today=await services.GetRequiredService<EmployeeShiftResolver>().TodayAsync();
 var employee=await services.GetRequiredService<EmployeeManager>().CreateEmployeeAsync(actor,role.Id,"Synthetic","Employee",null,"SE",EmployeeStatusType.Active,null,panNo:"ABCDE1234F",adharNo:"123412341234",dateOfJoining:today.AddDays(-100),employeeGroup:EmployeeGroupType.NonTeaching);
 await services.GetRequiredService<IEmployeeRepository>().InsertAsync(employee,true);employeeId=employee.Id;
 await unit.CompleteAsync();
}
using var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:4391/");
listener.Start();
Console.WriteLine("Shift browser bridge ready: disposable SQLite, real ABP HTTP endpoints, synthetic authentication.");
while (true)
{
	var context = await listener.GetContextAsync();
	try
	{
		if(context.Request.RawUrl == "/fixture/context")
   {
    context.Response.ContentType="application/json";
    await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.OutputStream,new { employeeId,today });
    context.Response.Close();continue;
   }
   var tenant = context.Request.Headers["Authorization"]?.Replace("Bearer ", "");
		if (tenant != a.ToString() && tenant != b.ToString())
		{
			context.Response.StatusCode = 401;
			context.Response.Close();
			continue;
		}
		using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.RawUrl);
		request.Headers.Add(TestAuthHandler.TestTenantIdHeader, tenant);
		if (context.Request.HasEntityBody)
		{
			using var reader = new StreamReader(context.Request.InputStream);
			request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, "application/json");
		}
		using var response = await client.SendAsync(request);
		context.Response.StatusCode = (int)response.StatusCode;
		context.Response.ContentType = "application/json";
		await context.Response.OutputStream.WriteAsync(await response.Content.ReadAsByteArrayAsync());
		context.Response.Close();
	}
	catch (Exception exception)
	{
		Console.WriteLine(exception.Message);
		context.Response.StatusCode = 500;
		context.Response.Close();
	}
}

class BrowserFactory : ShiftTenantTestFactory
{
	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseContentRoot(
			builder, Path.GetFullPath("src/ShikshaMitra.HttpApi.Host"));
	}
}
