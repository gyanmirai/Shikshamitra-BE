using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Identity;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Member_Charges_Should_Page_Actual_Settlement_Under_Its_Owning_Action()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var fixture = await SeedFinancePgAsync(factory);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		var collection = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		});
		collection.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collection.Receivable.OutstandingAmount.ShouldBe(60.50m);

		var route = $"/api/library/members/{fixture.EmployeeId:D}/charges";
		SetMemberChargeTestAction(maker, ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View);
		using var document = await ReadMemberChargeTestPageAsync(maker, route + "?page=1&pageSize=1");
		var page = document.RootElement;
		page.GetProperty("totalCount").GetInt32().ShouldBe(1);
		page.GetProperty("currentPage").GetInt32().ShouldBe(1);
		page.GetProperty("pageSize").GetInt32().ShouldBe(1);
		var row = page.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
		row.GetProperty("id").GetGuid().ShouldBe(fixture.Source.SourceId);
		row.GetProperty("amount").GetDecimal().ShouldBe(100.75m);
		row.GetProperty("amountText").GetString().ShouldBe("100.75");
		row.GetProperty("paymentStatus").GetInt32().ShouldBe(0);
		row.GetProperty("recordedCurrency").GetString().ShouldBe("INR");
		row.GetProperty("academicYearId").GetGuid().ShouldBe(factory.YearId);
		var position = row.GetProperty("financePosition");
		position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.Verified);
		position.GetProperty("approvalStatus").GetInt32().ShouldBe((int)ChargeStatus.Approved);
		position.GetProperty("currency").GetString().ShouldBe("INR");
		var amounts = position.GetProperty("amounts");
		amounts.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		amounts.GetProperty("collectibleAmountText").GetString().ShouldBe("60.50");
		position.TryGetProperty("financeChargeId", out _).ShouldBeFalse();
		using var beyond = await ReadMemberChargeTestPageAsync(maker, route + $"?page={int.MaxValue}&pageSize=100");
		beyond.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
		beyond.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);

		using var otherFinance = await maker.GetAsync(FinancePgRoute + FinancePgSourceQuery(fixture.Source));
		otherFinance.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		foreach (var action in new[]
		{
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View
		})
		{
			SetMemberChargeTestAction(maker, action);
			using var denied = await maker.GetAsync(route);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		}

		var noScopeActor = await InFinancePgAsync(factory, async services =>
		{
			var id = Guid.NewGuid();
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(
				new IdentityUser(id, $"unscoped-{id:N}", $"{id:N}@example.test", FinancePgTenantId), true);
			return id;
		});
		using var unscoped = FinancePgClient(factory, noScopeActor, "unscoped-library-reader");
		SetMemberChargeTestAction(unscoped, ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View);
		using var outOfScope = await unscoped.GetAsync(route);
		outOfScope.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await outOfScope.Content.ReadAsStringAsync());
		using var problem = JsonDocument.Parse(await outOfScope.Content.ReadAsStringAsync());
		problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(UserScopeErrorCodes.AccessDenied);
		problem.RootElement.TryGetProperty("items", out _).ShouldBeFalse();
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
	}

	private static void SetMemberChargeTestAction(HttpClient client, string action)
	{
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, action);
	}

	private static async Task<JsonDocument> ReadMemberChargeTestPageAsync(HttpClient client, string route)
	{
		using var response = await client.GetAsync(route);
		var body = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
		return JsonDocument.Parse(body);
	}
}
