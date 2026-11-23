using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

[Collection(ApiTestCollection.Name)]
public class ChargeCategoryDependencyApi_Tests(ShikshaMitraApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	[Fact]
	public async Task Unused_category_can_be_deleted_after_usage_check()
	{
		var id = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var usage = await Client.GetFromJsonAsync<ChargeCategoryUsageResponse>($"{ChargeCategoriesUrl}/{id}/usage", JsonOptions);

		usage.ShouldNotBeNull();
		usage.CategoryId.ShouldBe(id);
		usage.CategoryName.ShouldNotBeNullOrWhiteSpace();
		usage.ChargeCount.ShouldBe(0);
		usage.StudentAssignmentCount.ShouldBe(0);
		usage.CanDelete.ShouldBeTrue();
		(await Client.DeleteAsync($"{ChargeCategoriesUrl}/{id}")).IsSuccessStatusCode.ShouldBeTrue();
		(await Client.GetAsync($"{ChargeCategoriesUrl}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Delete_rechecks_charge_requests_added_since_usage_check()
	{
		var id = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var url = $"{ChargeCategoriesUrl}/{id}";
		(await Client.GetFromJsonAsync<ChargeCategoryUsageResponse>($"{url}/usage", JsonOptions))!.CanDelete.ShouldBeTrue();

		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var request = new ChargeRequest(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				"Charge awaiting assignment", id, "Dependency check", 100m, DateTimeOffset.UtcNow.AddDays(30),
				ChargePriority.Medium, Factory.SeededBoardId, Factory.SeededMediumId,
				Guid.Parse(TestAuthHandler.TestUserId), "{}");
			await scope.ServiceProvider.GetRequiredService<IRepository<ChargeRequest, Guid>>().InsertAsync(request);
			await uow.CompleteAsync();
		}

		var usage = await Client.GetFromJsonAsync<ChargeCategoryUsageResponse>($"{url}/usage", JsonOptions);
		usage.ShouldNotBeNull();
		usage.ChargeCount.ShouldBe(1);
		usage.StudentAssignmentCount.ShouldBe(0);
		usage.CanDelete.ShouldBeFalse();

		var deletion = await Client.DeleteAsync(url);
		deletion.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var problem = await deletion.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString().ShouldBe(FinanceChargeErrorCodes.ChargeCategoryInUse);
		problem.GetProperty("detail").GetString()!.ShouldContain(usage.CategoryName);
		(await Client.GetAsync(url)).IsSuccessStatusCode.ShouldBeTrue();
	}
}
