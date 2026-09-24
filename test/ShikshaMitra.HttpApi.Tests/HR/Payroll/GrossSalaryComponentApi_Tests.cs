using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class GrossSalaryComponentApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("earning", "Fixed", 60)]
	[InlineData("earning", "Variable", 0)]
	[InlineData("deduction", "Fixed", 5)]
	[InlineData("deduction", "Variable", 0)]
	public async Task Create_and_reopen_preserve_system_gross(string kind, string type, decimal percentage)
	{
		var request = Request(kind, type, percentage);
		var endpoint = $"/api/payroll/{kind}-components";
		var response = await Client.PostAsJsonAsync(endpoint, request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var created = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();
		var reopened = await Client.GetFromJsonAsync<JsonElement>($"{endpoint}/{created.Id}", JsonOptions);
		reopened.GetProperty("calculationBase").GetString().ShouldBe("GROSS");
		reopened.GetProperty("percentage").GetDecimal().ShouldBe(percentage);
		request["description"] = "Updated Gross calculation";
		request["percentage"] = type == "Fixed" ? percentage + 1m : percentage;
		var impactResponse = await Client.PostAsJsonAsync($"{endpoint}/{created.Id}/update-impact-preview", request, JsonOptions);
		impactResponse.EnsureSuccessStatusCode();
		var impact = await impactResponse.Content.ReadFromJsonAsync<PayrollComponentUpdateImpactPreviewDto>(JsonOptions);
		request["previewHash"] = impact!.PreviewHash;
		var updated = await Client.PutAsJsonAsync($"{endpoint}/{created.Id}", request, JsonOptions);
		updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
		reopened = await Client.GetFromJsonAsync<JsonElement>($"{endpoint}/{created.Id}", JsonOptions);
		reopened.GetProperty("calculationBase").GetString().ShouldBe("GROSS");
		reopened.GetProperty("percentage").GetDecimal().ShouldBe(type == "Fixed" ? percentage + 1m : percentage);

		reopened.GetProperty($"selected{char.ToUpperInvariant(kind[0])}{kind[1..]}ComponentId").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Theory]
	[InlineData("earning")]
	[InlineData("deduction")]
	public async Task Gross_cannot_also_reference_a_user_component(string kind)
	{
		var request = Request(kind, "Fixed", 5m);
		request[$"selected{char.ToUpperInvariant(kind[0])}{kind[1..]}ComponentId"] = Guid.NewGuid();
		var response = await Client.PostAsJsonAsync($"/api/payroll/{kind}-components", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static Dictionary<string, object?> Request(string kind, string type, decimal percentage) => new()
	{
		[$"{kind}ComponentName"] = $"Gross {kind} {Guid.NewGuid():N}",
		[$"{kind}Type"] = type,
		["calculationType"] = "Percentage",
		["calculationBase"] = "GROSS",
		["percentage"] = percentage,
		["visibleInPayslip"] = true
	};
}
