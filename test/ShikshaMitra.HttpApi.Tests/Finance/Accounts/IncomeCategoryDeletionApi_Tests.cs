using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class IncomeCategoryDeletionApi_Tests(FinanceRoutingApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Unused_income_category_can_be_deleted_without_deactivation(bool deactivate)
	{
		await EnsureAcademicScopeAsync();
		var created = await CreateCategoryAsync();
		var id = created.GetProperty("id").GetGuid();
		created.GetProperty("isActive").GetBoolean().ShouldBeTrue();
		created.GetProperty("isUsedInDownstream").GetBoolean().ShouldBeFalse();
		created.GetProperty("deletionBlockedReason").ValueKind.ShouldBe(JsonValueKind.Null);
		created.GetProperty("canDelete").GetBoolean().ShouldBeTrue();
		if (deactivate)
		{
			var response = await Client.PatchAsJsonAsync($"/api/income-categories/{id}/status",
				new { isActive = false, deactivationReason = "Unused test category", concurrencyStamp = created.GetProperty("concurrencyStamp").GetString() });
			response.EnsureSuccessStatusCode();
		}
		var detail = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}");
		detail.GetProperty("canDelete").GetBoolean().ShouldBeTrue();
		var list = await GetAndReadAsync<JsonElement>($"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}&search={Uri.EscapeDataString(created.GetProperty("name").GetString()!)}");
		list.GetProperty("items").EnumerateArray().Single().GetProperty("canDelete").GetBoolean().ShouldBeTrue();

		(await Client.DeleteAsync($"/api/income-categories/{id}")).EnsureSuccessStatusCode();
		(await Client.GetAsync($"/api/income-categories/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Deletion_rechecks_new_dependencies_and_preserves_referenced_category(bool activeFeeHead)
	{
		await EnsureAcademicScopeAsync();
		var category = await CreateCategoryAsync();
		var id = category.GetProperty("id").GetGuid();
		category.GetProperty("canDelete").GetBoolean().ShouldBeTrue();
		await WithTenantUowAsync(async provider =>
		{
			var categories = provider.GetRequiredService<IRepository<FeeCategory, Guid>>();
			var feeCategory = await categories.InsertAsync(new FeeCategory(Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId), Factory.SeededAcademicYearId, $"Deletion {Guid.NewGuid():N}", null), autoSave: true);
			var head = new FeeHead(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId, feeCategory.Id, "Linked fee head", null, false, null, id);
			if (!activeFeeHead) head.SetActiveStatus(false, "Historical dependency");
			await provider.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, autoSave: true);
		});

		var response = await Client.DeleteAsync($"/api/income-categories/{id}");
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:CategoryInUse");
		var detail = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}");
		detail.GetProperty("isUsedInDownstream").GetBoolean().ShouldBeTrue();
		detail.GetProperty("deletionBlockedReason").GetString().ShouldBe("Used by fee heads.");
		detail.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task A_charge_category_mapping_alone_blocks_income_category_deletion()
	{
		await EnsureAcademicScopeAsync();
		var created = await CreateCategoryAsync();
		var id = created.GetProperty("id").GetGuid();
		await WithTenantUowAsync(async provider =>
		{
			var chargeCategory = new ChargeCategory(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId, $"Mapped {Guid.NewGuid():N}", false, null);
			chargeCategory.SetAccountingMapping(id);
			await provider.GetRequiredService<IRepository<ChargeCategory, Guid>>()
				.InsertAsync(chargeCategory, autoSave: true);
		});

		var detail = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}");
		detail.GetProperty("isUsedInDownstream").GetBoolean().ShouldBeTrue();
		detail.GetProperty("deletionBlockedReason").GetString().ShouldBe("Used by charge categories.");
		detail.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
		var response = await Client.DeleteAsync($"/api/income-categories/{id}");
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:CategoryInUse");
	}

	[Theory]
	[InlineData(CollectionPolicyStatus.Active)]
	[InlineData(CollectionPolicyStatus.Draft)]
	public async Task A_collection_policy_mapping_alone_blocks_income_category_deletion(CollectionPolicyStatus status)
	{
		await EnsureAcademicScopeAsync();
		var created = await CreateCategoryAsync();
		var id = created.GetProperty("id").GetGuid();
		await WithTenantUowAsync(async provider =>
		{
			var model = new CollectionPolicyUpdateModelBuilder
			{
				PolicyName = $"Delete {Guid.NewGuid():N}", PolicyCode = Guid.NewGuid().ToString("N"), Status = status
			}.Build() with { LateFeeIncomeCategoryId = id };
			await provider.GetRequiredService<IRepository<CollectionPolicy, Guid>>()
				.InsertAsync(new CollectionPolicy(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
					Factory.SeededAcademicYearId, model), autoSave: true);
		});

		var response = await Client.DeleteAsync($"/api/income-categories/{id}");
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:CategoryInUse");
		var detail = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}");
		detail.GetProperty("deletionBlockedReason").GetString().ShouldBe("Used by collection policies.");
		detail.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task Income_with_a_transaction_cannot_be_deleted()
	{
		await EnsureAcademicScopeAsync();
		var category = await CreateCategoryAsync();
		var id = category.GetProperty("id").GetGuid();
		await WithTenantUowAsync(async provider =>
		{
			await provider.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>()
				.InsertAsync(new ShikshaMitra.Finance.Accounts.Incomes.Transaction(Guid.NewGuid(),
					Guid.Parse(TestAuthHandler.TestTenantId), Factory.SeededAcademicYearId, id,
					"Donation", [], 10m, 0, DateTime.UtcNow), autoSave: true);
		});

		var response = await Client.DeleteAsync($"/api/income-categories/{id}");
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:CategoryInUse");
		var detail = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}");
		detail.GetProperty("deletionBlockedReason").GetString().ShouldBe("Used by transactions.");
		detail.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task Income_view_permission_does_not_allow_deletion()
	{
		await EnsureAcademicScopeAsync();
		var category = await CreateCategoryAsync();
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"Finance.Accounts.Settings.IncomeCategory.View");
		(await viewer.DeleteAsync($"/api/income-categories/{category.GetProperty("id").GetGuid()}"))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Expense_categories_still_require_deactivation_before_deletion()
	{
		await EnsureAcademicScopeAsync();
		var category = await PostAndReadAsync<JsonElement>($"/api/expense-categories?academicYearId={Factory.SeededAcademicYearId}",
			new { name = $"Exp {Guid.NewGuid():N}", tags = Array.Empty<string>() });
		category.GetProperty("canDelete").GetBoolean().ShouldBeFalse();
		var response = await Client.DeleteAsync($"/api/expense-categories/{category.GetProperty("id").GetGuid()}");
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:ActiveRecordCannotBeDeleted");
	}

	private Task<JsonElement> CreateCategoryAsync() => PostAndReadAsync<JsonElement>(
		$"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}",
		new { name = $"Del {Guid.NewGuid():N}", tags = Array.Empty<string>() });

	private Task EnsureAcademicScopeAsync() => WithTenantUowAsync(async provider =>
	{
		var students = provider.GetRequiredService<IRepository<Student, Guid>>();
		if (!await students.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId))
			await students.InsertAsync(new Student(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				"Accounts", null, "Deletion", Factory.SeededAcademicYearId, Factory.SeededBoardId,
				Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId,
				Factory.SeededDivisionId), autoSave: true);
	});

	private async Task WithTenantUowAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var provider = scope.ServiceProvider;
		using (provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			await action(provider);
			await uow.CompleteAsync();
		}
	}
}
