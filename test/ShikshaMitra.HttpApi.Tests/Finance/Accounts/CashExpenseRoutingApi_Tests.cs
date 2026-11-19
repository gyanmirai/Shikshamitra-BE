using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class CashExpenseRoutingApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Scoped_admission_cash_writer_can_load_categories_and_pay_without_students_or_bank_access()
	{
		var year = Factory.SeededAcademicYearId;
		await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
		var collectedAt = await CashTestDates.InYearAsync(Factory.Services, year);
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.NewGuid();
		var categoryId = Guid.NewGuid();
		var excludedCategoryIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var cashId = Guid.NewGuid();
		var outsideCashId = Guid.NewGuid();
		var outsideStudentCashId = Guid.NewGuid();
		var bankId = Guid.NewGuid();
		var standardResponse = await Client.PostAsJsonAsync("/api/app/standard", new {
			name = "Cash " + Guid.NewGuid().ToString("N")[..8], ageLowerLimit = 5,
			educationMediumStageId = Factory.SeededMediumStageId });
		standardResponse.EnsureSuccessStatusCode();
		var standard = (await standardResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
		using (var serviceScope = Factory.Services.CreateScope())
		{
			var services = serviceScope.ServiceProvider;
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			(await services.GetRequiredService<IdentityUserManager>().CreateAsync(new IdentityUser(
				userId, "cash-writer-" + userId.ToString("N"), userId + "@example.test", tenantId))).Succeeded.ShouldBeTrue();
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), tenantId, userId: userId, academicYearId: year, educationBoardId: Factory.SeededBoardId,
				educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId, standardId: standard));
			var students = services.GetRequiredService<IRepository<Student, Guid>>();
			(await students.AnyAsync(x => x.AcademicYearId == year && x.StandardId == standard)).ShouldBeFalse();
			var outsideStudentId = Guid.NewGuid();
			await students.InsertAsync(new Student(outsideStudentId, tenantId, "Outside", null, "Cash", year,
				Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId));
			await services.GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(new BankAccount(
				bankId, year, "Cash routing bank", "Branch", null, "School", Enum.GetValues<BankAccountType>().First(),
				BitConverter.ToUInt64(bankId.ToByteArray(), 0).ToString(), "Routing regression", "TEST0001234", null, tenantId: tenantId));
			var categories = services.GetRequiredService<IRepository<TransactionCategory, Guid>>();
			await categories.InsertAsync(new TransactionCategory(categoryId, tenantId, year, TransactionType.Expense, "Cash " + categoryId, null));
			await categories.InsertAsync(new TransactionCategory(excludedCategoryIds[0], tenantId, year, TransactionType.Expense, "Inactive " + categoryId, null, isActive: false));
			await categories.InsertAsync(new TransactionCategory(excludedCategoryIds[1], tenantId, year, TransactionType.Expense, "Future " + categoryId, null, effectiveFrom: DateTimeOffset.UtcNow.AddDays(1)));
			await categories.InsertAsync(new TransactionCategory(excludedCategoryIds[2], tenantId, year, TransactionType.Income, "Income " + categoryId.ToString("N")[..8], null));
			foreach (var (sourceId, classId) in new[] { (cashId, standard), (outsideCashId, Factory.SeededStandardId) })
			{
				var accountId = Guid.NewGuid();
				await services.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(new FinanceFeeAccount(
					accountId, tenantId, Guid.NewGuid(), Guid.NewGuid(), year, Factory.SeededBoardId,
					Factory.SeededMediumId, Factory.SeededMediumStageId, classId, "Admission applicant"));
				var cash = new CashCollection(sourceId, tenantId, year, Guid.NewGuid(), "collection:" + sourceId,
					null, "CASH-" + sourceId, "Admission applicant", 100m, collectedAt,
					Guid.Parse(TestAuthHandler.TestUserId), null, accountId);
				await services.GetRequiredService<IRepository<CashCollection, Guid>>().InsertAsync(cash);
			}
			var outsideStudentCash = new CashCollection(outsideStudentCashId, tenantId, year, Guid.NewGuid(),
				"collection:" + outsideStudentCashId, null, "CASH-" + outsideStudentCashId, "Outside student", 100m,
				collectedAt, Guid.Parse(TestAuthHandler.TestUserId), outsideStudentId, null);
			await services.GetRequiredService<IRepository<CashCollection, Guid>>().InsertAsync(outsideStudentCash);
			await uow.CompleteAsync();
		}
		using var writer = CreateClientAs(userId);
		writer.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		writer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.ExpenseTracking.Add");
		var routingUrl = $"/api/finance-routing-options?academicYearId={year}&operation=cash-expense";
		var routingResponse = await writer.GetAsync(routingUrl);
		routingResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await routingResponse.Content.ReadAsStringAsync());
		var routing = await routingResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		routing.GetProperty("expenseCategories").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == categoryId).ShouldBeTrue();
		routing.GetProperty("expenseCategories").EnumerateArray().Any(x => excludedCategoryIds.Contains(x.GetProperty("id").GetGuid())).ShouldBeFalse();
		routing.GetProperty("incomeCategories").GetArrayLength().ShouldBe(0);
		routing.GetProperty("bankAccounts").GetArrayLength().ShouldBe(0);
		(await writer.GetAsync(routingUrl.Replace("cash-expense", "expense-entry"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var sources = await writer.GetFromJsonAsync<JsonElement>($"/api/finance/cash-management/options?academicYearId={year}&operation=expense", JsonOptions);
		var source = sources.GetProperty("cashSources").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == cashId);
		sources.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == outsideCashId).ShouldBeFalse();
		sources.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == outsideStudentCashId).ShouldBeFalse();
		sources.GetProperty("bankAccounts").GetArrayLength().ShouldBe(0);
		object Expense(Guid id, string stamp) => new { idempotencyKey = Guid.NewGuid(), name = "Admission cash expense " + Guid.NewGuid(),
			counterparty = "Supplier", categoryId, amount = 10m, paymentMethod = 0, paymentDate = collectedAt.AddMinutes(2),
			categoryTags = Array.Empty<string>(), cashCollectionId = id, cashConcurrencyStamp = stamp, paymentReference = "BILL-1" };
		var paid = await writer.PostAsJsonAsync($"/api/expenses?academicYearId={year}", Expense(cashId, source.GetProperty("concurrencyStamp").GetString()!), JsonOptions);
		paid.IsSuccessStatusCode.ShouldBeTrue(await paid.Content.ReadAsStringAsync());
		(await writer.PostAsJsonAsync($"/api/expenses?academicYearId={year}", Expense(outsideCashId, "irrelevant"), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await writer.PostAsJsonAsync($"/api/expenses?academicYearId={year}", Expense(outsideStudentCashId, "irrelevant"), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await writer.GetAsync(routingUrl.Replace(year.ToString(), Guid.NewGuid().ToString()))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		writer.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		writer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.CashManagement.View");
		(await writer.GetAsync(routingUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		writer.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		writer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.ExpenseTracking.Add");
		writer.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		Assert.Contains((await writer.GetAsync(routingUrl)).StatusCode,
			new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
	}
}
