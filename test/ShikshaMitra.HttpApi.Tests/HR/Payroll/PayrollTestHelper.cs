using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// Shared helper for creating payroll-related test entities via the API.
/// </summary>
public static class PayrollTestHelper
{
	public static async Task<Compensation.SaveEmployeeCompensationDto> BindEsiPolicyVersionAsync(
		HttpClient client, Compensation.SaveEmployeeCompensationDto input)
	{
		if (input.EsiManagementMode == EsiManagement.External) return input;
		var policies = await client.GetFromJsonAsync<EsiPolicyDto[]>("/api/payroll/statutory-schemes/esi/policies/options", JsonOptions);
		var policy = policies!.Single(x => input.EsiPolicyId.HasValue ? x.Id == input.EsiPolicyId : x.IsDefault);
		input.EsiPolicyId = policy.Id;
		input.ExpectedEsiPolicyVersionId = policy.Versions.Where(x => x.EffectiveFrom <= input.EffectiveMonth &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo >= input.EffectiveMonth))
			.OrderByDescending(x => x.EffectiveFrom).First().Id;
		return input;
	}

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

	public static async Task<PayrollComponentSeed> CreatePercentageEarningComponentAsync(
		HttpClient client,
		Guid selectedEarningComponentId,
		decimal percentage,
		string? nameSuffix = null)
	{
		var name = TestDataHelper.UniqueName(nameSuffix ?? "PctEarnHelper");
		var input = new EarningComponentCreateRequestDto
		{
			EarningComponentName = name,
			EarningType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = percentage,
			SelectedEarningComponentId = selectedEarningComponentId,
			VisibleInPayslip = true,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/earning-components", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var dto = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create percentage earning component");
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

	public static async Task<PayrollComponentSeed> CreatePercentageDeductionComponentAsync(
		HttpClient client,
		Guid selectedComponentId,
		decimal percentage,
		string? nameSuffix = null)
	{
		var name = TestDataHelper.UniqueName(nameSuffix ?? "PctDeductHelper");
		var input = new DeductionComponentCreateRequestDto
		{
			DeductionComponentName = name,
			DeductionType = "FIXED",
			CalculationType = "PERCENTAGE",
			Percentage = percentage,
			SelectedDeductionComponentId = selectedComponentId,
			VisibleInPayslip = true,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/deduction-components", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		var dto = await response.Content.ReadFromJsonAsync<ComponentCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create percentage deduction component");
		return new PayrollComponentSeed(dto.Id, name);
	}

	public static async Task<PfRuleCreateResponseDto> CreatePfRuleAsync(
		HttpClient client, string? nameSuffix = null)
	{
		var input = new CreateUpdateProvidentFundDto
		{
			Name = TestDataHelper.UniqueName(nameSuffix ?? "PFRule"),
			Code = BuildValidPfCode("PF"),
			EmployeePercentage = 12m,
			EmployerPercentage = 12m,
		};

		var response = await client.PostAsJsonAsync("/api/payroll/pf-rules", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<PfRuleCreateResponseDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create PF rule");
	}

	public static async Task EnsureActiveEsiTestSetupAsync(ShikshaMitraApiTestFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var establishmentRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<StatutoryEstablishment, Guid>>();
		var treatmentRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<PayrollComponentStatutoryTreatment, Guid>>();
		var earningRepository = scope.ServiceProvider.GetRequiredService<IEarningComponentRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var unitOfWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var establishment = await establishmentRepository.FirstOrDefaultAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
			if (establishment is null)
			{
				establishment = new StatutoryEstablishment(
					Guid.NewGuid(), tenantId, StatutorySchemeCodes.EmployeeStateInsurance, "IN-GJ");
				await establishmentRepository.InsertAsync(establishment, autoSave: false);
			}
			establishment.Configure(
				"IN-GJ",
				"TEST-ESI-REGISTRATION",
				50,
				new DateTime(2020, 1, 1),
				new DateTime(2020, 1, 1),
				"HTTP API salary test setup");
			establishment.Activate(Guid.Parse(TestAuthHandler.TestUserId), DateTime.UtcNow);
			await establishmentRepository.UpdateAsync(establishment, autoSave: false);

			var existingTreatments = await treatmentRepository.GetListAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
			var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
			foreach (var earning in await earningRepository.GetListAsync(x => x.IsActive))
			{
				var currentTreatment = existingTreatments
					.Where(x => x.ComponentId == earning.Id && x.EffectiveFrom <= currentMonth &&
						(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= currentMonth))
					.OrderByDescending(x => x.EffectiveFrom)
					.FirstOrDefault();
				if (currentTreatment is not null)
				{
					currentTreatment.Update(
						StatutoryWageTreatment.Included,
						currentTreatment.EffectiveFrom,
						currentTreatment.EffectiveTo);
					await treatmentRepository.UpdateAsync(currentTreatment, autoSave: false);
					continue;
				}
				await treatmentRepository.InsertAsync(
					new PayrollComponentStatutoryTreatment(
						Guid.NewGuid(), tenantId, earning.Id,
						StatutorySchemeCodes.EmployeeStateInsurance,
						StatutoryWageTreatment.Included,
						currentMonth),
					autoSave: false);
			}
			await unitOfWork.CompleteAsync();
		}
	}

	private static string BuildValidPfCode(string prefix)
	{
		var raw = $"{prefix}_{Guid.NewGuid():N}".ToUpperInvariant();
		var chars = raw.Select(c =>
		{
			if (char.IsLetter(c) || c == '_')
			{
				return c;
			}

			return (char)('A' + (c - '0'));
		}).ToArray();

		return new string(chars)[..20];
	}
}
