using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Infrastructure.Traits;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeTableFilterApi_Tests(FinanceRoutingApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Amount_Header_Filter_Is_Applied_Before_Counting_And_Paging()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var search = $"Filter-{Guid.NewGuid():N}";
		foreach (var amount in new[] { 50m, 150m })
		{
			var input = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
			input.ChargeName = search;
			input.Amount = amount;
			var created = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}", input, JsonOptions);
			created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		}
		var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { new { column = "amount", type = "greaterThan", value = 100 } }));
		var url = $"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}&status=0&search={search}&pageSize=1";
		var filtered = await Client.GetFromJsonAsync<ChargeListResponse>($"{url}&filters={filters}", JsonOptions);
		filtered.ShouldNotBeNull();
		filtered.TotalCount.ShouldBe(1);
		filtered.Items.Single().Amount.ShouldBe(150m);
		var next = await Client.GetFromJsonAsync<ChargeListResponse>($"{url}&filters={filters}&page=2", JsonOptions);
		next.ShouldNotBeNull();
		next.TotalCount.ShouldBe(1);
		next.Items.ShouldBeEmpty();
		var cleared = await Client.GetFromJsonAsync<ChargeListResponse>(url, JsonOptions);
		cleared.ShouldNotBeNull();
		cleared.TotalCount.ShouldBe(2);
	}
}
