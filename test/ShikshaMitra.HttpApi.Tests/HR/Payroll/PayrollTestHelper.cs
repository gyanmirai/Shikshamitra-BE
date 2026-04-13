using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Data;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// Shared helper for creating payroll-related test entities via the API.
/// </summary>
public static class PayrollTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public sealed record PayrollComponentSeed(Guid Id, string Name);

	public static async Task<PayrollComponentSeed> CreateEarningComponentAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var name = TestDataHelper.UniqueName(nameSuffix ?? "EarnHelper");
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = name,
			EarningType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 30000m,
			VisibleInPayslip = true,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/earning-components", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var dto = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create earning component");
		return new PayrollComponentSeed(dto.Id, name);
	}

	public static async Task<PayrollComponentSeed> CreateDeductionComponentAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var name = TestDataHelper.UniqueName(nameSuffix ?? "DeductHelper");
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = name,
			DeductionType = "FIXED",
			CalculationType = "AMOUNT",
			Amount = 3000m,
			VisibleInPayslip = true,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/deduction-components", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var dto = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create deduction component");
		return new PayrollComponentSeed(dto.Id, name);
	}

	public static async Task<PfRuleCreateResponseDto> CreatePfRuleAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var input = new CreateUpdateProvidentFundDto
		{
			Name = TestDataHelper.UniqueName(nameSuffix ?? "PFRule"),
			Code = TestDataHelper.UniqueName("PF").Replace("_", "").ToUpperInvariant()[..12],
			EmployeePercentage = 12m,
			EmployerPercentage = 12m,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/pf-rules", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<PfRuleCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create PF rule");
	}
}
