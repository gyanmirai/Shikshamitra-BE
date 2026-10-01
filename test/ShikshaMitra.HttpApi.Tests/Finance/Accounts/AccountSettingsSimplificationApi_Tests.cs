using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Migrations;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(ApiTestCollection.Name)]
public class AccountSettingsSimplificationApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("Income", "income-categories", "Add")]
	[InlineData("Income", "income-categories", "Edit")]
	[InlineData("Expense", "expense-categories", "Add")]
	[InlineData("Expense", "expense-categories", "Edit")]
	public async Task Category_editor_can_select_only_active_heads_of_its_type(string type, string route, string action)
	{
		var head = await CreateHeadAsync(type);
		var other = await CreateHeadAsync(type == "Income" ? "Expense" : "Income");
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"Finance.Accounts.Settings.{type}Category.{action}");
		var response = await client.GetAsync($"/api/{route}/account-head-options?academicYearId={Factory.SeededAcademicYearId}");
		response.EnsureSuccessStatusCode();
		var options = await response.Content.ReadFromJsonAsync<JsonElement>();
		var ids = options.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
		ids.ShouldContain(head.GetProperty("id").GetGuid());
		ids.ShouldNotContain(other.GetProperty("id").GetGuid());
		var status = await Client.PatchAsJsonAsync($"/api/account-heads/{head.GetProperty("id").GetGuid()}/status", new
		{
			isActive = false, deactivationReason = "Lookup regression", concurrencyStamp = head.GetProperty("concurrencyStamp").GetString()
		});
		status.EnsureSuccessStatusCode();
		response = await client.GetAsync($"/api/{route}/account-head-options?academicYearId={Factory.SeededAcademicYearId}");
		response.EnsureSuccessStatusCode();
		options = await response.Content.ReadFromJsonAsync<JsonElement>();
		options.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ShouldNotContain(head.GetProperty("id").GetGuid());
	}

	[Theory]
	[InlineData("income-categories", "Income", "Expense")]
	[InlineData("expense-categories", "Expense", "Income")]
	public async Task View_and_other_category_grants_cannot_access_editor_lookup(string route, string type, string other)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"Finance.Accounts.Settings.{type}Category.View,Finance.Accounts.Settings.{other}Category.Add");
		var response = await client.GetAsync($"/api/{route}/account-head-options?academicYearId={Factory.SeededAcademicYearId}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("Income", "income-categories")]
	[InlineData("Expense", "expense-categories")]
	public async Task Simplified_forms_create_and_edit_without_removed_fields(string type, string route)
	{
		var head = await CreateHeadAsync(type);
		foreach (var field in new[] { "headCode", "currencyCode", "postingAllowed", "isControlAccount", "effectiveFrom", "effectiveTo" })
			head.TryGetProperty(field, out _).ShouldBeFalse();
		var headId = head.GetProperty("id").GetGuid();
		var headUpdate = await Client.PutAsJsonAsync($"/api/account-heads/{headId}", new
		{
			headName = $"Updated {Guid.NewGuid():N}", headType = type,
			concurrencyStamp = head.GetProperty("concurrencyStamp").GetString()
		});
		headUpdate.EnsureSuccessStatusCode();
		var category = await PostAndReadAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new
		{
			name = $"Category {Guid.NewGuid():N}", defaultAccountHeadId = headId, tags = new[] { "Fees" }
		});
		var code = category.GetProperty("code").GetString();
		code.ShouldNotBeNullOrWhiteSpace();
		category.GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ShouldContain("fees");
		category.GetProperty("effectiveFrom").ValueKind.ShouldBe(JsonValueKind.Null);
		var update = await Client.PutAsJsonAsync($"/api/{route}/{category.GetProperty("id").GetGuid()}", new
		{
			name = $"Updated category {Guid.NewGuid():N}", defaultAccountHeadId = headId,
			tags = new[] { "Fees" }, concurrencyStamp = category.GetProperty("concurrencyStamp").GetString()
		});
		update.EnsureSuccessStatusCode();
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("code").GetString().ShouldBe(code);
		updated.GetProperty("defaultAccountHeadId").GetGuid().ShouldBe(headId);
		updated.GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ShouldContain("fees");
		var wrongHead = await CreateHeadAsync(type == "Income" ? "Expense" : "Income");
		var invalid = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new
		{
			name = $"Invalid {Guid.NewGuid():N}", defaultAccountHeadId = wrongHead.GetProperty("id").GetGuid(), tags = new[] { "Fees" }
		});
		invalid.IsSuccessStatusCode.ShouldBeFalse();
		(await invalid.Content.ReadAsStringAsync()).ShouldContain("Finance:Accounts:Settings:");
	}

	[Fact]
	public async Task Bank_account_generates_code_and_preserves_it_when_editor_omits_code()
	{
		var head = await CreateHeadAsync("Asset");
		var accountHeadId = head.GetProperty("id").GetGuid();
		var accountNumber = Random.Shared.NextInt64(100000000000, 999999999999).ToString();
		var bank = await PostAndReadAsync<JsonElement>($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}", new
		{
			accountHeadId, bankName = "Example School Bank", branchName = "Main Branch",
			accountHolderName = "Example School", accountType = 0, accountNumber,
			ifscCode = "SBIN0123456"
		});
		var code = bank.GetProperty("code").GetString();
		code.ShouldNotBeNullOrWhiteSpace();
		code!.Length.ShouldBeLessThanOrEqualTo(32);
		bank.GetProperty("accountHeadId").GetGuid().ShouldBe(accountHeadId);

		var update = await Client.PutAsJsonAsync($"/api/bank-accounts/{bank.GetProperty("id").GetGuid()}", new
		{
			accountHeadId, bankName = "Example School Bank Updated", branchName = "Main Branch",
			accountHolderName = "Example School", accountType = 0, accountNumber,
			ifscCode = "SBIN0123456", concurrencyStamp = bank.GetProperty("concurrencyStamp").GetString()
		});
		update.EnsureSuccessStatusCode();
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("code").GetString().ShouldBe(code);
	}

	[Fact]
	public void Migration_archives_legacy_values_before_dropping_only_the_removed_columns()
	{
		Migration migration = new SimplifyAccountHeads { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		migration.UpOperations.First().ShouldBeOfType<SqlOperation>().Sql.ShouldContain("LegacyAccountHeadSettings20260909");
		var columns = migration.UpOperations.OfType<DropColumnOperation>().ToList();
		columns.Count.ShouldBe(7);
		columns.ShouldAllBe(x => x.Table == "Finance_AccountHeads");
		columns.Select(x => x.Name).ShouldNotContain("Id");
		migration.TargetModel.FindEntityType("ShikshaMitra.Domain.Finance.Accounts.Settings.AccountHead")!
			.GetProperties().Select(x => x.Name).ShouldNotContain("PostingAllowed");
		migration.DownOperations.OfType<AddColumnOperation>().Count().ShouldBe(7);
	}

	private async Task<JsonElement> CreateHeadAsync(string type)
	{
		// Finance scope is currently resolved through students. The shared fixture deliberately skips student seeding.
		using (var scope = Factory.Services.CreateScope())
		{
			var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			using (tenant.Change(tenantId))
			using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
			{
				var students = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
				if (!await students.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId))
					await students.InsertAsync(new Student(Guid.NewGuid(), tenantId, "Accounts", null, "Test",
						Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
						Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId), autoSave: true);
				await uow.CompleteAsync();
			}
		}
		var response = await Client.PostAsJsonAsync($"/api/account-heads?academicYearId={Factory.SeededAcademicYearId}",
			new { headName = $"{type} {Guid.NewGuid():N}", headType = type });
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}
}
