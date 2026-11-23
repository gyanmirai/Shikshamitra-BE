using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

[Collection(ApiTestCollection.Name)]
public class RefundPolicyCloneApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/finance/refund-policies";

	[Fact]
	public async Task Same_year_copies_keep_selected_fee_heads_and_get_distinct_bounded_names()
	{
		var references = await SeedReferencesAsync();
		var sourceName = "Selected fee head " + Guid.NewGuid().ToString("N").PadRight(110, 'a');
		var source = await CreatePolicyAsync(new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = references.YearId,
			PolicyName = sourceName,
			FeeHeadScope = "SelectedFeeHeads",
			ApplicableFeeHeadIds = [references.FeeHeadId],
			ExpenseCategoryId = references.ExpenseCategoryId,
			Status = "Active",
			IsDefault = true,
			AllowedPayoutMethods = ["Cash"],
			AmountMode = "Percentage",
			PercentageValue = 75m
		});

		var first = await ReadPolicyAsync(await CloneAsync(source.Id, references.YearId));
		var second = await ReadPolicyAsync(await CloneAsync(source.Id, references.YearId));

		first.PolicyName.Length.ShouldBe(128);
		first.PolicyName.ShouldEndWith(" Copy");
		second.PolicyName.ShouldEndWith(" Copy 2");
		second.PolicyCode.ShouldNotBe(first.PolicyCode);
		first.PolicyCode.ShouldNotBe(source.PolicyCode);
		first.AcademicYearId.ShouldBe(source.AcademicYearId);
		first.FeeHeadScope.ShouldBe(source.FeeHeadScope);
		first.ApplicableFeeHeadIds.ShouldBe(source.ApplicableFeeHeadIds);
		first.ExpenseCategoryId.ShouldBe(source.ExpenseCategoryId);
		first.AllowedPayoutMethods.ShouldBe(source.AllowedPayoutMethods);
		first.PercentageValue.ShouldBe(75m);
		first.Status.ShouldBe("Draft");
		first.IsDefault.ShouldBeFalse();
		var currentSource = await Client.GetFromJsonAsync<RefundPolicyDto>($"{BaseUrl}/{source.Id}", JsonOptions);
		currentSource!.IsDefault.ShouldBeTrue();
		var versions = await Client.GetFromJsonAsync<PagedResultDto<FinancePolicyVersionDto>>($"{BaseUrl}/{first.Id}/versions",
			JsonOptions);
		versions!.Items.Single().ChangeAction.ShouldBe("cloned");
	}

	[Fact]
	public async Task Cross_year_copy_preserves_rules_and_excludes_source_year_routing()
	{
		var references = await SeedReferencesAsync();
		var source = await CreatePolicyAsync(new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = references.YearId,
			PolicyName = $"Transferable policy {Guid.NewGuid():N}",
			ExpenseCategoryId = references.ExpenseCategoryId,
			IncludePaidLateFee = true,
			AmountMode = "FixedAmount",
			FixedAmount = 1000m,
			AllowedPayoutMethods = ["Cash", "BankTransfer"]
		});

		var clone = await ReadPolicyAsync(await CloneAsync(source.Id, Factory.SeededAcademicYearId));

		clone.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		clone.Status.ShouldBe("Draft");
		clone.IsDefault.ShouldBeFalse();
		clone.FeeHeadScope.ShouldBe("AllFeeHeads");
		clone.IncludePaidLateFee.ShouldBeTrue();
		clone.FixedAmount.ShouldBe(1000m);
		clone.AllowedPayoutMethods.ShouldBe(source.AllowedPayoutMethods);
		clone.ExpenseCategoryId.ShouldBeNull();
	}

	[Fact]
	public async Task Cross_year_copy_requires_target_year_fee_heads_for_selected_scope()
	{
		var references = await SeedReferencesAsync();
		var source = await CreatePolicyAsync(new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = references.YearId,
			PolicyName = $"Selected policy {Guid.NewGuid():N}",
			FeeHeadScope = "SelectedFeeHeads",
			ApplicableFeeHeadIds = [references.FeeHeadId]
		});

		var response = await CloneAsync(source.Id, Factory.SeededAcademicYearId);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString().ShouldBe("Finance:RefundPolicy:010030");
		problem.GetProperty("detail").GetString()!.ShouldContain("selected fee heads");
	}

	[Fact]
	public async Task Duplicate_and_oversized_copy_names_return_actionable_errors()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var source = await CreatePolicyAsync(new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			PolicyName = $"Copy validation {Guid.NewGuid():N}"
		});

		var duplicate = await CloneAsync(source.Id, source.AcademicYearId, source.PolicyName);
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var conflict = await duplicate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		conflict.GetProperty("errorCode").GetString().ShouldBe(RefundPolicyErrorCodes.DuplicateName);
		conflict.GetProperty("detail").GetString()!.ShouldContain(source.PolicyName);

		var tooLong = await CloneAsync(source.Id, source.AcademicYearId, new string('a', 129));
		tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var invalid = await tooLong.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		invalid.GetProperty("errorCode").GetString().ShouldBe(RefundPolicyErrorCodes.InvalidCloneName);
		invalid.GetProperty("detail").GetString()!.ShouldContain("3 and 128");
	}

	private Task<HttpResponseMessage> CloneAsync(Guid sourceId, Guid targetYear, string? name = null) =>
		Client.PostAsJsonAsync($"{BaseUrl}/{sourceId}/clone", new { targetAcademicYearId = targetYear, name }, JsonOptions);

	private async Task<RefundPolicyDto> CreatePolicyAsync(CreateUpdateRefundPolicyDto input) =>
		await ReadPolicyAsync(await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions));

	private async Task<RefundPolicyDto> ReadPolicyAsync(HttpResponseMessage response)
	{
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<RefundPolicyDto>(JsonOptions))!;
	}

	private async Task<(Guid YearId, Guid FeeHeadId, Guid ExpenseCategoryId)> SeedReferencesAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var scope = Factory.Services.CreateScope();
		var provider = scope.ServiceProvider;
		using (provider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var year = await provider.GetRequiredService<AcademicYearManager>().CreateAsync(
				$"Clone QA {Guid.NewGuid():N}", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
			var feeCategory = new FeeCategory(Guid.NewGuid(), tenantId, year.Id, "Policy clone fee category", null);
			var income = new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Income,
				"Policy clone income", null);
			var expense = new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Expense,
				"Policy clone expense", null);
			var feeHead = new FeeHead(Guid.NewGuid(), tenantId, year.Id, feeCategory.Id, "Policy clone tuition",
				null, false, null, income.Id);
			await provider.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year);
			await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income);
			await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(expense);
			await provider.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(feeCategory);
			await provider.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(feeHead);
			await uow.CompleteAsync();
			return (year.Id, feeHead.Id, expense.Id);
		}
	}
}
