using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private const string FinancePgRoute = "/api/finance/external-receivables";
	private static readonly Guid FinancePgTenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid FinancePgMakerId = Guid.Parse(TestAuthHandler.TestUserId);
	private const string FinancePgMakerRole = "pg-finance-maker";

	// Permission headers exercise the existing API harness; no scope or grant-denial claims
	// are made by this positive PostgreSQL persistence/immutable-history workflow.
	private static string FinancePgActions => string.Join(',',
		ShikshaMitraSubmodulePermissions.Library.Charges.View,
		ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
		ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse);

	private static HttpClient FinancePgClient(LibraryPostgresApiTestFactory factory, Guid actorId, string role)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actorId.ToString("D"));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, role);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, FinancePgActions);
		return client;
	}

	private static async Task<T> FinancePgGetAsync<T>(HttpClient client, string path)
	{
		using var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
			?? throw new InvalidOperationException($"The response from {path} was empty.");
	}

	private static string FinancePgSourceQuery(FinanceExternalSourceReference source) =>
		$"?kind={(int)source.Kind}&sourceId={source.SourceId:D}&sourceVersion={Uri.EscapeDataString(source.SourceVersion)}";

	private static async Task<T> InFinancePgAsync<T>(LibraryPostgresApiTestFactory factory,
		Func<IServiceProvider, Task<T>> action)
	{
		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(FinancePgTenantId);
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var result = await action(services);
		await unit.CompleteAsync();
		return result;
	}

	private static Task<FinancePgFixture> SeedFinancePgAsync(LibraryPostgresApiTestFactory factory, bool includeCurrentLibraryMember = false) =>
		InFinancePgAsync(factory, async services =>
		{
			services.GetRequiredService<IClock>().Now.Kind.ShouldBe(DateTimeKind.Utc);
			var roles = services.GetRequiredService<IIdentityRoleRepository>();
			var users = services.GetRequiredService<IIdentityUserRepository>();
			var makerRole = new IdentityRole(Guid.NewGuid(), FinancePgMakerRole, FinancePgTenantId);
			await roles.InsertAsync(makerRole, true);
			var maker = await users.GetAsync(FinancePgMakerId);
			maker.AddRole(makerRole.Id);
			await users.UpdateAsync(maker, true);

			var reviewerRole = await roles.FindByNormalizedNameAsync("ADMIN");
			if (reviewerRole == null)
				reviewerRole = await roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", FinancePgTenantId), true);
			var reviewerId = Guid.NewGuid();
			var reviewer = new IdentityUser(reviewerId, $"pg-reviewer-{reviewerId:N}",
				$"{reviewerId:N}@example.test", FinancePgTenantId);
			reviewer.AddRole(reviewerRole.Id);
			await users.InsertAsync(reviewer, true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), FinancePgTenantId, userId: reviewerId, academicYearId: factory.YearId,
				description: "Independent persisted school administrator for PostgreSQL Finance proof"), true);
			var userScope = services.GetRequiredService<IUserScopeService>();
			await userScope.InvalidateUserScopeCacheAsync(FinancePgMakerId);
			await userScope.InvalidateUserScopeCacheAsync(reviewerId);
			(await userScope.IsAdminUserAsync(reviewerId)).ShouldBeTrue();
			(await userScope.IsAdminUserAsync(FinancePgMakerId)).ShouldBeFalse();

			// HTTP tests cannot use the internal Employee constructor. Use the same
			// public creation manager and assignable-role contract as HR API tests.
			var staffRole = new IdentityRole(Guid.NewGuid(), "PG Library staff", FinancePgTenantId);
			staffRole.SetProperty(EmployeeRoleAudience.ExtraPropertyName, EmployeeRoleAudience.Employee);
			staffRole.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, true);
			staffRole.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.NonTeaching);
			staffRole.SetProperty("Code", "LPG");
			await roles.InsertAsync(staffRole, true);
			var employeeUserId = Guid.NewGuid();
			var employeeAccount = new IdentityUser(employeeUserId, $"pg-staff-{employeeUserId:N}",
				$"{employeeUserId:N}@example.test", FinancePgTenantId);
			employeeAccount.AddRole(staffRole.Id);
			await users.InsertAsync(employeeAccount, true);
			var employee = await services.GetRequiredService<EmployeeCreationManager>().CreateEmployeeAsync(
				employeeUserId, staffRole.Id, "Maya", "José", null, "MJ",
				includeCurrentLibraryMember ? EmployeeStatusType.Active : EmployeeStatusType.Inactive, null,
				email: employeeAccount.Email, panNo: "ABCDE1234F", adharNo: "123412341234",
				dateOfJoining: new DateOnly(2026, 4, 1), employeeGroup: EmployeeGroupType.NonTeaching);
			await services.GetRequiredService<IEmployeeRepository>().InsertAsync(employee, true);
			if (includeCurrentLibraryMember)
				await services.GetRequiredService<IEmployeeAdditionalDetailsRepository>().InsertAsync(new EmployeeAdditionalDetails(
					Guid.NewGuid(), FinancePgTenantId, employee.Id, null, false, null, libraryMember: true, transportMember: false), true);

			var category = new LibraryChargeCategory(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				"Reviewed PG damage", 100.75m, false, "Checked original Library damage evidence.");
			await services.GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			var assessment = new LibraryCharge(Guid.NewGuid(), employee.Id, 1, category.Id, null, 100.75m,
				new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
				new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc), FinancePgTenantId)
				{ Remarks = "Reviewed original staff assessment; no Student or fee assignment." };
			assessment.ConfirmSourceContext(factory.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { AssessmentId = assessment.Id, AcademicYearId = factory.YearId,
					ReviewedBy = FinancePgMakerId, Reason = "Checked the original assessment register and its school year." }),
				new(2026, 11, 17), new(2026, 11, 24),
				LibraryCivilDateProvenance.ReviewedLegacy, LibraryCivilDateProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { ReviewedBy = FinancePgMakerId, Reason = "Checked original school dates." }), "INR");
			await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(assessment, true);
			var income = new TransactionCategory(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				TransactionType.Income, "Library PG recoveries", null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
			var financeCategory = new ChargeCategory(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				"PG Library damage", true, null);
			financeCategory.SetAccountingMapping(income.Id);
			await services.GetRequiredService<IRepository<ChargeCategory, Guid>>().InsertAsync(financeCategory, true);
			var expense = new TransactionCategory(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				TransactionType.Expense, "Recorded PG Library refund", null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(expense, true);
			var bank = new BankAccount(Guid.NewGuid(), factory.YearId, "School Refund Bank", "School branch", null,
				"Configured School", BankAccountType.Current, "1234567890", "Recorded refund payout", "TEST0000001",
				null, tenantId: FinancePgTenantId);
			await services.GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(bank, true);

			var collection = new CollectionPolicy(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				new CollectionPolicyUpdateModel(
					PolicyName: "Explicit PG collection", PolicyCode: "PG-COLLECT", Description: "Confirmed source collection",
					Status: CollectionPolicyStatus.Active, IsDefault: false, EffectiveFrom: null, EffectiveTo: null,
					EnableLateFee: false, GracePeriodDays: 0, LateFeeApplyOnFeeHeads: [], LateFeeType: LateFeeCalculationType.Flat,
					FlatAmount: null, Percentage: null, PercentageBase: PercentageBaseType.OutstandingAmount,
					AmountPerDay: null, AmountPerWeek: null, AmountPerMonth: null, CompoundFrequency: CompoundFrequencyType.Daily,
					EscalationRules: [], FeeHeadOverrides: [], RoundingMethod: RoundingMethodType.None,
					ConsiderAsIncome: false, LateFeeIncomeHeadId: null, MaximumLateFeeAmount: null, MaximumChargingDays: null,
					EnableWaiver: false, EnableChargeWaiver: true, RequiresApproval: false, ApprovalRoleIds: [], AllowOnlineCollection: true));
			await services.GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(collection, true);
			var collectionVersion = new FinancePolicyVersion(Guid.NewGuid(), FinancePgTenantId, "collection", collection.Id,
				factory.YearId, collection.Version, collection.PolicyName, collection.Status.ToString(), collection.ToSnapshot().Serialize(),
				"created", "Confirmed PostgreSQL collection rules.");
			var refund = new RefundPolicy(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
				new RefundPolicyUpdateModel("Explicit PG refund", "PG-REFUND", null, RefundPolicyStatus.Active, false,
					RefundFeeHeadScope.AllFeeHeads, [], true, false, true, RefundAmountMode.Percentage, 50m, null, null, null,
					RoundingMethodType.None, RefundDeductionType.None, null, null, null,
					[RefundPayoutMethod.Cash, RefundPayoutMethod.Upi], null));
			await services.GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(refund, true);
			var refundVersion = new FinancePolicyVersion(Guid.NewGuid(), FinancePgTenantId, "refund", refund.Id,
				factory.YearId, refund.Version, refund.PolicyName, refund.Status.ToString(), JsonSerializer.Serialize(refund.ToSnapshot()),
				"created", "Confirmed PostgreSQL paid-charge refund rules.");
			var versions = services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
			await versions.InsertAsync(collectionVersion, true);
			await versions.InsertAsync(refundVersion, true);
			return new FinancePgFixture(employee.Id, reviewerId, reviewerRole.Name, financeCategory.Id, expense.Id, bank.Id,
				collectionVersion.Id, refundVersion.Id, new FinanceExternalSourceReference
				{
					Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = assessment.Id,
					SourceVersion = assessment.GetAssessmentVersion()
				});
		});

	private static async Task<FinancePgPrepared> PrepareFinancePgAsync(LibraryPostgresApiTestFactory factory,
		LibraryMigrationPostgresFixture database, FinancePgFixture fixture)
	{
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var input = new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = fixture.ChargeCategoryId, IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var posted = await PostAsync<FinanceExternalReceivableDto>(maker, FinancePgRoute, input);
		var read = await FinancePgGetAsync<FinanceExternalReceivableDto>(maker, FinancePgRoute + FinancePgSourceQuery(fixture.Source));
		read.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		read.Payer.EmployeeId.ShouldBe(fixture.EmployeeId);
		read.OriginalAmount.ShouldBe(100.75m);
		var replay = await PostAsync<FinanceExternalReceivableDto>(maker, FinancePgRoute, input);
		replay.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		posted.Payer.EmployeeId.ShouldBe(fixture.EmployeeId);
		posted.Payer.StudentId.ShouldBeNull();
		var linkId = await InFinancePgAsync(factory, async services =>
			(await services.GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.SourceId == fixture.Source.SourceId)).Single().Id);
		var originalLink = await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", linkId);
		var originalPost = await database.ScalarAsync($"""
			SELECT to_jsonb(t)::text FROM "Finance_ExternalReceivableCommands" t
			WHERE "SourceLinkId"='{linkId:D}' AND "Operation"='Post';
			""");
		originalPost.ShouldNotBeNullOrWhiteSpace();
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		await PostAsync<FinanceExternalCollectionFormDto>(maker, FinancePgRoute + "/settlement-policy",
			new BindFinanceExternalSettlementPolicyInput
			{
				Source = fixture.Source, Revision = form.Revision, CollectionPolicyVersionId = fixture.CollectionPolicyVersionId,
				RefundPolicyVersionId = fixture.RefundPolicyVersionId, Reason = "Explicitly confirmed source settlement policies.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			});
		using var reviewer = FinancePgClient(factory, fixture.ReviewerId, fixture.ReviewerRole);
		form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(reviewer,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		var approval = new FinanceExternalApprovalInput
		{
			Source = fixture.Source, Revision = form.Revision, Reason = "Independently checked original damage and amount.",
			IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		var approved = await PostAsync<FinanceExternalReceivableDto>(reviewer, FinancePgRoute + "/approve", approval);
		approved.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
		(await PostAsync<FinanceExternalReceivableDto>(reviewer, FinancePgRoute + "/approve", approval))
			.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		return new(fixture, posted, linkId, originalLink!, originalPost!);
	}

	private static async Task<string> FinancePgRowAsync(LibraryMigrationPostgresFixture database, string table, Guid id)
	{
		// All callers supply a source-owned fixed table name and a typed Guid.
		var row = await database.ScalarAsync($"SELECT to_jsonb(t)::text FROM \"{table}\" t WHERE \"Id\"='{id:D}';");
		row.ShouldNotBeNullOrWhiteSpace();
		return row!;
	}

	private static async Task<ShikshaMitraDbContext> FinancePgDbAsync(IServiceProvider services) =>
		await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();

	private sealed record FinancePgFixture(Guid EmployeeId, Guid ReviewerId, string ReviewerRole, Guid ChargeCategoryId,
		Guid ExpenseCategoryId, Guid BankId, Guid CollectionPolicyVersionId, Guid RefundPolicyVersionId,
		FinanceExternalSourceReference Source);
	private sealed record FinancePgPrepared(FinancePgFixture Fixture, FinanceExternalReceivableDto Posted,
		Guid LinkId, string OriginalLinkJson, string OriginalPostJson);
}

