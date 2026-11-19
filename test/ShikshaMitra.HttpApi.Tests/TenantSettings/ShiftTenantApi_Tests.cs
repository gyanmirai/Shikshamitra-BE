using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class ShiftTenantTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
			[new("Logging:LogLevel:Default", "Warning")]));
	}

	public async Task SeedTenantAsync(Guid id, string name)
	{
		using var scope = Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var entity = await scope.ServiceProvider.GetRequiredService<ITenantManager>().CreateAsync(name);
		typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(entity, id);
		await scope.ServiceProvider.GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(entity);
		await unit.CompleteAsync();
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class ShiftTenantApi_Tests : IClassFixture<ShiftTenantTestFactory>
{
	private const string Url = "/api/app/shift-management";
	private readonly ShiftTenantTestFactory _factory;
	public ShiftTenantApi_Tests(ShiftTenantTestFactory factory) => _factory = factory;

	[Fact]
	public async Task Http_should_scope_creation_pagination_mutations_and_forged_references()
	{
		var tenantA = Guid.NewGuid();
		var tenantB = Guid.NewGuid();
		await _factory.SeedTenantAsync(tenantA, "shift-a-" + tenantA);
		await _factory.SeedTenantAsync(tenantB, "shift-b-" + tenantB);
		using var a = Client(tenantA);
		using var b = Client(tenantB);
		using var host = Client(null);
		var legacy = await CreateAsync(host, "Legacy host shift");
		var first = await CreateAsync(a, "Mayur shift", tenantB);
		var second = await CreateAsync(a, "DDVS full day");
		var foreign = await CreateAsync(b, "Swaminarayan own shift");
		a.DefaultRequestHeaders.Add("__tenant", tenantB.ToString());
		var page = (await a.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>(Url + "?maxResultCount=1&sorting=ShiftName"))!;
		page.TotalCount.ShouldBe(2);
		page.Items.Count.ShouldBe(1);
		var bList = (await b.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>(Url))!;
		bList.TotalCount.ShouldBe(1);
		bList.Items[0].Id.ShouldBe(foreign.Id);
		foreach (var id in new[] { first.Id, second.Id, legacy.Id })
		{
			(await b.GetAsync(Url + "/" + id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			(await b.PutAsJsonAsync(Url + "/" + id, Input("Forged update"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			(await b.DeleteAsync(Url + "/" + id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		}
		var standard = await b.PostAsJsonAsync("/api/app/standard", new {
			name = "Forged standard", ageLowerLimit = 6, educationMediumStageId = Guid.NewGuid(), shiftId = first.Id });
		standard.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await standard.Content.ReadAsStringAsync());
		(await standard.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
		(await a.GetFromJsonAsync<ShiftManagementDto>(Url + "/" + first.Id))!.ShiftName.ShouldBe("Mayur shift");
		(await b.PutAsJsonAsync(Url + "/" + foreign.Id, Input("Updated own shift"))).IsSuccessStatusCode.ShouldBeTrue();
		(await b.GetFromJsonAsync<ShiftManagementDto>(Url + "/" + foreign.Id))!.ShiftName.ShouldBe("Updated own shift");
		(await b.DeleteAsync(Url + "/" + foreign.Id)).IsSuccessStatusCode.ShouldBeTrue();
		(await b.GetFromJsonAsync<PagedResultDto<ShiftManagementDto>>(Url))!.TotalCount.ShouldBe(0);
		using var anonymous = Client(tenantA);
		anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	private HttpClient Client(Guid? tenant)
	{
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant?.ToString() ?? "host");
		return client;
	}
	private static object Input(string name, Guid? forgedTenant = null) => new {
		shiftName = name, startTime = "08:00:00", endTime = "16:00:00", tenantId = forgedTenant };
	private static async Task<ShiftManagementDto> CreateAsync(HttpClient client, string name, Guid? forgedTenant = null)
	{
		var response = await client.PostAsJsonAsync(Url, Input(name, forgedTenant));
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<ShiftManagementDto>())!;
	}
}
