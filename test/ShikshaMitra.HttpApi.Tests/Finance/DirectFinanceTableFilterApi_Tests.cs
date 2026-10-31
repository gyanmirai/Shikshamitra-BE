using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class DirectFinanceTableFilterApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static object Criterion(string column, string type, object value) => new { column, type, value };

	[Fact]
	public async Task Fee_head_headers_filter_display_values_before_count_and_page_and_preserve_year()
	{
		var context = await SeedSettingsAsync(TenantId);
		var url = $"/api/fee-heads?academicYearId={context.YearId}&pageSize=1";
		await AssertAsync(Client, url, 1, context.HeadId, Criterion("name", "endsWith", "target"));
		foreach (var criterion in new[]
		{
			Criterion("feeCategoryName", "equals", "Target Category"),
			Criterion("incomeCategoryName", "contains", "Target Income"),
			Criterion("description", "equals", "-"),
			Criterion("isActive", "equals", 0)
		}) await AssertAsync(Client, url, 1, context.HeadId, criterion);
		await AssertAsync(Client, url, 0, null, Criterion("name", "contains", "absent"));
		await AssertAsync(Client, url, 1, context.HeadId, Criterion("name", "equals", "absent"), Criterion("name", "endsWith", "target"));
		await AssertAsync(Client, url + "&page=2", 1, null, Criterion("name", "endsWith", "target"));
		await AssertAsync(Client, url + "&search=absent", 0, null, Criterion("isActive", "equals", 0));
		await AssertAsync(Client, url + $"&feeCategoryId={context.OtherCategoryId}", 0, null, Criterion("isActive", "equals", 0));
		await AssertAsync(Client, url, 102, null);
		await AssertAsync(Client, url + "&filters=%5B%5D", 102, null);
		var foreign = await SeedSettingsAsync(Guid.NewGuid());
		await AssertAsync(Client, $"/api/fee-heads?academicYearId={foreign.YearId}", 0, null, Criterion("name", "endsWith", "target"));
	}

	[Fact]
	public async Task Receipt_book_headers_filter_text_number_boolean_and_combined_board_before_paging()
	{
		var context = await SeedSettingsAsync(TenantId);
		var url = $"/api/receipt-books?academicYearId={context.YearId}&pageSize=1";
		await AssertAsync(Client, url, 1, context.BookId, Criterion("startingNumber", "greaterThan", 900));
		foreach (var criterion in new[]
		{
			Criterion("name", "endsWith", "target"), Criterion("prefix", "equals", "000042"),
			Criterion("suffix", "equals", "Z"), Criterion("board", "equals", "CBSE/English"),
			Criterion("mediumStageName", "equals", "Primary"), Criterion("description", "equals", "Target description"),
			Criterion("endingNumber", "between", new { min = 999, max = 1001 }), Criterion("isActive", "equals", 0)
		}) await AssertAsync(Client, url, 1, context.BookId, criterion, Criterion("startingNumber", "greaterThan", 900));
		await AssertAsync(Client, url, 0, null, Criterion("prefix", "contains", "absent"));
		await AssertAsync(Client, url + "&page=2", 1, null, Criterion("startingNumber", "greaterThan", 900));
		await AssertAsync(Client, url + "&search=absent", 0, null, Criterion("startingNumber", "greaterThan", 900));
		await AssertAsync(Client, url, 102, null);
		var foreign = await SeedSettingsAsync(Guid.NewGuid());
		await AssertAsync(Client, $"/api/receipt-books?academicYearId={foreign.YearId}", 0, null, Criterion("name", "endsWith", "target"));
	}

	[Fact]
	public async Task Adjustment_headers_and_visible_search_cover_records_after_first_hundred_with_row_scope()
	{
		var context = await SeedAdjustmentsAsync(TenantId);
		var url = $"/api/fee-adjustments/applications?academicYearId={context.YearId}&boardId={context.BoardId}&educationMediumId={context.MediumId}&pageSize=1";
		await AssertAsync(Client, url, 1, context.TargetId, Criterion("adjustmentAmount", "greaterThan", 900));
		foreach (var criterion in new[]
		{
			Criterion("studentName", "equals", "Target Student (456789)"),
			Criterion("className", "equals", "Visible / A"), Criterion("rollNumber", "equals", ""),
			Criterion("requestedBy", "equals", "Filter Requester"), Criterion("createdAt", "equals", "2026-06-12"),
			Criterion("status", "equals", "Pending")
		}) await AssertAsync(Client, url, 1, context.TargetId, criterion, Criterion("adjustmentAmount", "greaterThan", 900));
		foreach (var search in new[] { "Target Student", "456789", "TARGET-VOUCHER", "target reason" })
			await AssertAsync(Client, url + "&filter=" + Uri.EscapeDataString(search), 1, context.TargetId);
		await AssertAsync(Client, url + "&filter=absent", 0, null);
		await AssertAsync(Client, url + "&status=1", 0, null, Criterion("adjustmentAmount", "greaterThan", 900));
		await AssertAsync(Client, url + "&page=2", 1, null, Criterion("adjustmentAmount", "greaterThan", 900));
		await AssertAsync(Client, url, 103, null);
		using var restricted = CreateClientAs(context.UserId);
		restricted.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		await AssertAsync(restricted, url, 102, null, Criterion("adjustmentAmount", "greaterThan", 1));
		await AssertAsync(restricted, url, 1, context.TargetId, Criterion("studentName", "contains", "456789"));
		await AssertAsync(restricted, url + "&filter=Hidden", 0, null);
		var foreign = await SeedAdjustmentsAsync(Guid.NewGuid());
		await AssertAsync(Client, $"/api/fee-adjustments/applications?academicYearId={foreign.YearId}&boardId={foreign.BoardId}&educationMediumId={foreign.MediumId}", 0, null,
			Criterion("adjustmentAmount", "greaterThan", 1), allowForbidden: true);
	}

	[Fact]
	public async Task Direct_list_headers_reject_malformed_and_unsupported_criteria_before_empty_results()
	{
		// Board/medium access requires an existing scoped student; minimal foundation seeds do not create one.
		var context = await SeedAccessibleContextAsync();
		var year = context.YearId;
		var adjustmentUrl = $"/api/fee-adjustments/applications?academicYearId={year}&boardId={context.BoardId}&educationMediumId={context.MediumId}&filter=absent";
		await AssertAsync(Client, adjustmentUrl, 0, null); // Prove authorization before testing malformed input.
		var urls = new[] { $"/api/fee-heads?academicYearId={year}&search=absent", $"/api/receipt-books?academicYearId={year}&search=absent", adjustmentUrl };
		foreach (var url in urls)
		foreach (var filters in new[] { "{", "null", "[null]", "[{\"column\":null,\"type\":\"equals\",\"value\":\"x\"}]", "[{\"column\":\"secret\",\"type\":\"equals\",\"value\":\"x\"}]" })
		{
			var response = await Client.GetAsync(url + "&filters=" + Uri.EscapeDataString(filters));
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		}
	}

	private static async Task AssertAsync(HttpClient client, string url, int count, Guid? target, object? filter = null, object? second = null, bool allowForbidden = false)
	{
		var filters = new[] { filter, second }.Where(x => x != null).ToArray();
		var response = await client.GetAsync(url + (filters.Length == 0 ? "" : "&filters=" + Uri.EscapeDataString(JsonSerializer.Serialize(filters))));
		if (allowForbidden && response.StatusCode == HttpStatusCode.Forbidden) return;
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		result.GetProperty("totalCount").GetInt32().ShouldBe(count, url + " " + JsonSerializer.Serialize(filters));
		if (target.HasValue) result.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(target.Value);
		else if (count == 0 || url.Contains("&page=2")) result.GetProperty("items").GetArrayLength().ShouldBe(0);
	}

	private async Task<Context> SeedAccessibleContextAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var context = await SeedContextAsync(services, TenantId);
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), TenantId, "Validation", null, "Student",
			context.YearId, context.BoardId, context.MediumId, context.StageId, context.StandardId, context.DivisionId,
			DateTimeOffset.UtcNow.AddYears(-12), GenderEnum.Male, "9876543210");
		await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student);
		await uow.CompleteAsync();
		return context;
	}

	private async Task<Context> SeedContextAsync(IServiceProvider services, Guid tenantId)
	{
		async Task<T> Save<T>(T entity) where T : class, IEntity<Guid> => await services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
		var year = await Save(await services.GetRequiredService<AcademicYearManager>().CreateAsync("Direct " + Guid.NewGuid().ToString("N"), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)));
		var board = await Save(await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE, year.StartDate, year.EndDate));
		var medium = await Save(await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
		var stage = await Save(await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
		var standard = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Visible", 6, null));
		var division = await Save(await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 40));
		return new Context(year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id);
	}

	private async Task<SettingsContext> SeedSettingsAsync(Guid tenantId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var context = await SeedContextAsync(services, tenantId);
		async Task<T> Save<T>(T entity) where T : class, IEntity<Guid> => await services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
		var category = await Save(new FeeCategory(Guid.NewGuid(), tenantId, context.YearId, "Target Category", null));
		var other = await Save(new FeeCategory(Guid.NewGuid(), tenantId, context.YearId, "Other Category", null));
		var account = await Save(new AccountHead(Guid.NewGuid(), tenantId, context.YearId, "Filter Revenue", AccountHeadType.Income, null, null));
		var income = await Save(new TransactionCategory(Guid.NewGuid(), tenantId, context.YearId, TransactionType.Income,
			"Target Income", null, [], true, "DIR-" + Guid.NewGuid().ToString("N")[..8], account.Id, DateTimeOffset.UtcNow.AddDays(-1), null));
		var otherIncome = await Save(new TransactionCategory(Guid.NewGuid(), tenantId, context.YearId, TransactionType.Income,
			"Other Income", null, [], true, "DIR-" + Guid.NewGuid().ToString("N")[..8], account.Id, DateTimeOffset.UtcNow.AddDays(-1), null));
		var headId = Guid.NewGuid(); var bookId = Guid.NewGuid();
		for (var i = 0; i < 102; i++)
		{
			var target = i == 0;
			await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new FeeHead(target ? headId : Guid.NewGuid(), tenantId, context.YearId, target ? category.Id : other.Id,
				target ? "Direct target" : $"Direct {i}", null, false, target ? null : "Other", target ? income.Id : otherIncome.Id, !target)
				{ CreationTime = new DateTime(2026, 1, 1).AddMinutes(i) });
			await services.GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(new ReceiptBook(target ? bookId : Guid.NewGuid(), tenantId, context.YearId, context.BoardId, context.MediumId, context.StageId,
				target ? "Direct target" : $"Direct {i}", target ? "000042" : "OTHER", target ? "Z" : "Y", target ? 999 : 1,
				target ? 1000 : 100, target ? "Target description" : "Other", !target) { CreationTime = new DateTime(2026, 1, 1).AddMinutes(i) });
		}
		await uow.CompleteAsync();
		return new SettingsContext(context.YearId, headId, bookId, other.Id);
	}

	private async Task<AdjustmentContext> SeedAdjustmentsAsync(Guid tenantId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var context = await SeedContextAsync(services, tenantId);
		async Task<T> Save<T>(T entity) where T : class, IEntity<Guid> => await services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
		var userId = Guid.NewGuid();
		var user = new IdentityUser(userId, "direct-" + userId.ToString("N"), userId.ToString("N") + "@example.test", tenantId) { Name = "Filter Requester" };
		await Save(user);
		await Save(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, academicYearId: context.YearId,
			educationBoardId: context.BoardId, educationMediumId: context.MediumId, educationMediumStageId: context.StageId, standardId: context.StandardId));
		var hidden = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(context.StageId, "Hidden", 6, null));
		var hiddenDivision = await Save(await services.GetRequiredService<DivisionManager>().CreateAsync(hidden.Id, "B", 40));
		var targetId = Guid.NewGuid();
		for (var i = 0; i < 103; i++)
		{
			var target = i == 0; var isHidden = i == 102;
			var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, target ? "Target" : isHidden ? "Hidden" : "Other", null, "Student",
				context.YearId, context.BoardId, context.MediumId, context.StageId, isHidden ? hidden.Id : context.StandardId,
				isHidden ? hiddenDivision.Id : context.DivisionId, DateTimeOffset.UtcNow.AddYears(-12), GenderEnum.Male, "9876543210");
			if (target) student.AssignGrNo(456789);
			await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student);
			var request = (FeeAdjustmentRequest)Activator.CreateInstance(typeof(FeeAdjustmentRequest), BindingFlags.Instance | BindingFlags.NonPublic,
				null, [target ? targetId : Guid.NewGuid(), tenantId, student.Id, context.YearId, context.BoardId, context.MediumId,
					isHidden ? hidden.Id : context.StandardId, isHidden ? hiddenDivision.Id : context.DivisionId, target ? "target reason" : "other reason",
					target ? "TARGET-VOUCHER" : "DIRECT-" + Guid.NewGuid().ToString("N")[..12], target ? 999d : 100d], CultureInfo.InvariantCulture)!;
			request.CreationTime = target ? new DateTime(2026, 6, 12) : new DateTime(2026, 6, 13).AddMinutes(i);
			request.CreatorId = userId;
			await services.GetRequiredService<IRepository<FeeAdjustmentRequest, Guid>>().InsertAsync(request);
		}
		await uow.CompleteAsync();
		return new AdjustmentContext(context.YearId, context.BoardId, context.MediumId, targetId, userId);
	}

	private sealed record Context(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid StandardId, Guid DivisionId);
	private sealed record SettingsContext(Guid YearId, Guid HeadId, Guid BookId, Guid OtherCategoryId);
	private sealed record AdjustmentContext(Guid YearId, Guid BoardId, Guid MediumId, Guid TargetId, Guid UserId);
}
