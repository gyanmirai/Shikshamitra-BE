using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalReceivableApi_Tests
{
	[Fact]
	public async Task External_Collection_Http_Should_Bind_A_Quoted_Decimal_Amount_Without_Changing_The_Real_Allocation()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		var authority = await SeedWirePolicyAndReviewerAsync();
		using var maker = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add));
		var posted = await WirePostAsync<FinanceExternalReceivableDto>(maker, BaseUrl, new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = fixture.FinanceCategoryId,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		posted.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		var form = await WireGetAsync<FinanceExternalCollectionFormDto>(maker, BaseUrl + "/collect-form" + SourceQuery(fixture.Source));
		await WirePostAsync<FinanceExternalCollectionFormDto>(maker, BaseUrl + "/settlement-policy", new BindFinanceExternalSettlementPolicyInput
		{
			Source = fixture.Source, Revision = form.Revision, CollectionPolicyVersionId = authority.CollectionPolicyVersionId,
			Reason = "Confirmed the explicit source collection rules.", IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		using var reviewer = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve));
		reviewer.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, authority.ReviewerId.ToString());
		reviewer.DefaultRequestHeaders.Remove(TestAuthHandler.TestRolesHeader);
		reviewer.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, authority.ReviewerRoleName);
		form = await WireGetAsync<FinanceExternalCollectionFormDto>(reviewer, BaseUrl + "/collect-form" + SourceQuery(fixture.Source));
		var approved = await WirePostAsync<FinanceExternalReceivableDto>(reviewer, BaseUrl + "/approve", new FinanceExternalApprovalInput
		{
			Source = fixture.Source, Revision = form.Revision, Reason = "Independently checked the reviewed assessment.",
			IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		approved.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
		form = await WireGetAsync<FinanceExternalCollectionFormDto>(maker, BaseUrl + "/collect-form" + SourceQuery(fixture.Source));
		form.CanCollect.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		var key = Guid.NewGuid().ToString("N");
		var collected = await WirePostAsync<FinanceExternalCollectionDto>(maker, BaseUrl + "/collections", new
		{
			source = fixture.Source, revision = form.Revision, idempotencyKey = key, schoolReceiptDate = "2026-11-17",
			payments = new[] { new { paymentMethod = (int)PaymentMethodEnum.Cash, amount = "40.25" } }
		});

		collected.CollectionId.ShouldNotBe(Guid.Empty);
		collected.ReceiptId.ShouldNotBe(Guid.Empty);
		collected.AcceptedAmount.ShouldBe(40.25m);
		collected.SchoolReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
		collected.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
		collected.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.NotApplicable);
		collected.Receivable.Source.SourceId.ShouldBe(fixture.Source.SourceId);
		collected.Receivable.Source.SourceVersion.ShouldBe(fixture.Source.SourceVersion);
		collected.Receivable.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		collected.Receivable.Payer.StudentId.ShouldBe(fixture.StudentId);
		collected.Receivable.Payer.EmployeeId.ShouldBeNull();
		collected.Receivable.OriginalAmount.ShouldBe(100.75m);
		collected.Receivable.AcceptedPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.PendingVerificationAmount.ShouldBe(0m);
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);
		await InSchoolAsync(async services =>
		{
			var header = await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(collected.CollectionId);
			header.TotalPaidFee.ShouldBe(40.25m);
			header.StudentId.ShouldBe(fixture.StudentId);
			header.EmployeeId.ShouldBeNull();
			header.FinanceFeeAccountId.ShouldBeNull();
			header.Status.ShouldBe(FeeCollectionStatus.Paid);
			var allocation = (await services.GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
				.GetListAsync(row => row.FeeRecordMetadataId == collected.CollectionId)).Single();
			allocation.Amount.ShouldBe(40.25m);
			allocation.ChargeId.ShouldBe(posted.FinanceChargeId);
			allocation.StudentId.ShouldBe(fixture.StudentId);
			allocation.EmployeeId.ShouldBeNull();
			var receipt = await services.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(collected.ReceiptId);
			receipt.FeeCollectionTransactionId.ShouldBe(header.Id);
			receipt.TotalPaidAmount.ShouldBe(40.25m);
			receipt.ReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
			receipt.StudentId.ShouldBe(fixture.StudentId);
			receipt.EmployeeId.ShouldBeNull();
			(await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.CountAsync(row => row.StudentId == fixture.StudentId)).ShouldBe(0);
		});
	}

	[Fact]
	public async Task External_Receivable_Http_Should_Preserve_The_Supported_Finance_Boundary_As_A_Json_Number()
	{
		const decimal amount = 9_999_998.99m;
		(amount <= FinanceChargeAmountRules.MaximumAmount).ShouldBeTrue();
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		await InSchoolAsync(async services =>
		{
			var charges = services.GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var assessment = await charges.GetAsync(fixture.Source.SourceId);
			assessment.ApplyUpdate(amount, null, null, null, null, DateTime.UtcNow);
			await charges.UpdateAsync(assessment, autoSave: true);
			fixture.Source.SourceVersion = assessment.GetAssessmentVersion();
		});
		using var maker = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View));
		using var postedResponse = await maker.PostAsJsonAsync(BaseUrl, new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = fixture.FinanceCategoryId,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}, JsonOptions);
		postedResponse.IsSuccessStatusCode.ShouldBeTrue(await postedResponse.Content.ReadAsStringAsync());
		using var posted = JsonDocument.Parse(await postedResponse.Content.ReadAsStringAsync());
		var chargeId = posted.RootElement.GetProperty("financeChargeId").GetGuid();
		chargeId.ShouldNotBe(Guid.Empty);
		posted.RootElement.GetProperty("source").GetProperty("sourceId").GetGuid().ShouldBe(fixture.Source.SourceId);
		posted.RootElement.GetProperty("payer").GetProperty("studentId").GetGuid().ShouldBe(fixture.StudentId);
		using var readResponse = await maker.GetAsync(BaseUrl + SourceQuery(fixture.Source));
		readResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readResponse.Content.ReadAsStringAsync());
		using var read = JsonDocument.Parse(await readResponse.Content.ReadAsStringAsync());
		foreach (var response in new[] { posted.RootElement, read.RootElement })
		{
			var original = response.GetProperty("originalAmount");
			original.ValueKind.ShouldBe(JsonValueKind.Number);
			original.GetRawText().ShouldBe("9999998.99");
			original.GetDecimal().ShouldBe(amount);
			FinanceMoneyPrecision.FromLegacyDouble(original.GetDouble()).ShouldBe(amount);
			response.GetProperty("financeChargeId").GetGuid().ShouldBe(chargeId);
			response.GetProperty("assessmentDate").GetString().ShouldBe("2026-11-17");
			response.GetProperty("dueDate").GetString().ShouldBe("2026-11-24");
			foreach (var property in new[] { "acceptedPaymentAmount", "settledPaymentAmount", "pendingVerificationAmount", "waivedAmount", "refundedAmount", "outstandingAmount" })
			{
				response.GetProperty(property).ValueKind.ShouldBe(JsonValueKind.Number);
				response.GetProperty(property).GetDecimal().ShouldBe(0m);
			}
		}
		await InSchoolAsync(async services =>
		{
			(await services.GetRequiredService<IRepository<Charge, Guid>>().GetAsync(chargeId)).Amount.ShouldBe(amount);
			(await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId)).Amount.ShouldBe(amount);
			(await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.CountAsync(row => row.StudentId == fixture.StudentId)).ShouldBe(0);
		});
	}

	[Fact]
	public async Task External_Receivable_Http_Should_Reject_An_Over_Limit_Source_Before_Creating_Finance_Rows()
	{
		const decimal amount = 10_000_000.01m;
		(amount > FinanceChargeAmountRules.MaximumAmount).ShouldBeTrue();
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		await InSchoolAsync(async services =>
		{
			var charges = services.GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var assessment = await charges.GetAsync(fixture.Source.SourceId);
			assessment.ApplyUpdate(amount, null, null, null, null, DateTime.UtcNow);
			await charges.UpdateAsync(assessment, autoSave: true);
			fixture.Source.SourceVersion = assessment.GetAssessmentVersion();
		});
		using var maker = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add));
		using var response = await maker.PostAsJsonAsync(BaseUrl, new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = fixture.FinanceCategoryId,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		error.RootElement.GetProperty("errorCode").GetString().ShouldBe(LibraryFinanceErrorCodes.InvalidAmount);
		await InSchoolAsync(async services =>
		{
			(await services.GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.CountAsync(row => row.SourceId == fixture.Source.SourceId)).ShouldBe(0);
			(await services.GetRequiredService<IRepository<ChargeRequest, Guid>>()
				.CountAsync(row => row.ChargeCategoryId == fixture.FinanceCategoryId)).ShouldBe(0);
			(await services.GetRequiredService<IRepository<Charge, Guid>>()
				.CountAsync(row => row.StudentId == fixture.StudentId)).ShouldBe(0);
			var assessment = await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId);
			assessment.Amount.ShouldBe(amount);
			assessment.FinanceSourceLinkId.ShouldBeNull();
			assessment.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		});
	}

	private async Task<WireAuthority> SeedWirePolicyAndReviewerAsync()
	{
		WireAuthority? result = null;
		await InSchoolAsync(async services =>
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			var reviewerId = Guid.NewGuid();
			reviewerId.ShouldNotBe(Guid.Parse(TestAuthHandler.TestUserId));
			var role = new IdentityRole(Guid.NewGuid(), $"wire-reviewer-{reviewerId:N}", tenantId);
			await services.GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
			var reviewer = new IdentityUser(reviewerId, $"wire-reviewer-{reviewerId:N}", $"{reviewerId:N}@example.test", tenantId);
			reviewer.AddRole(role.Id);
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(reviewer, true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), tenantId, userId: reviewerId, academicYearId: Factory.SeededAcademicYearId,
				description: "Independent Finance wire-test reviewer for the source year"), true);
			await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(reviewerId);
			var policy = new CollectionPolicy(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId, new CollectionPolicyUpdateModel(
				PolicyName: "Explicit wire collection", PolicyCode: "WIRE-COLLECT", Description: "Reviewed source collection rules",
				Status: CollectionPolicyStatus.Active, IsDefault: false, EffectiveFrom: null, EffectiveTo: null,
				EnableLateFee: false, GracePeriodDays: 0, LateFeeApplyOnFeeHeads: [], LateFeeType: LateFeeCalculationType.Flat,
				FlatAmount: null, Percentage: null, PercentageBase: PercentageBaseType.OutstandingAmount,
				AmountPerDay: null, AmountPerWeek: null, AmountPerMonth: null, CompoundFrequency: CompoundFrequencyType.Daily,
				EscalationRules: [], FeeHeadOverrides: [], RoundingMethod: RoundingMethodType.None,
				ConsiderAsIncome: false, LateFeeIncomeHeadId: null, MaximumLateFeeAmount: null, MaximumChargingDays: null,
				EnableWaiver: false, EnableChargeWaiver: true, RequiresApproval: false, ApprovalRoleIds: [], AllowOnlineCollection: true));
			await services.GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(policy, true);
			var version = new FinancePolicyVersion(Guid.NewGuid(), tenantId, "collection", policy.Id, Factory.SeededAcademicYearId,
				policy.Version, policy.PolicyName, policy.Status.ToString(), policy.ToSnapshot().Serialize(), "created", "Confirmed collection wire-test policy");
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
			result = new(reviewerId, role.Name, version.Id);
		});
		return result ?? throw new InvalidOperationException("The real source reviewer and policy were not seeded.");
	}

	private static async Task<T> WirePostAsync<T>(HttpClient client, string route, object input)
	{
		using var response = await client.PostAsJsonAsync(route, input, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
			?? throw new InvalidOperationException($"The response from {route} was empty.");
	}

	private static async Task<T> WireGetAsync<T>(HttpClient client, string route)
	{
		using var response = await client.GetAsync(route);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
			?? throw new InvalidOperationException($"The response from {route} was empty.");
	}

	private sealed record WireAuthority(Guid ReviewerId, string ReviewerRoleName, Guid CollectionPolicyVersionId);
}
