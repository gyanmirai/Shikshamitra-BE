using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class LeavePolicyApi_Tests : ShikshaMitraApiTestBase
{
	private const string LeavePoliciesUrl = "/api/leave-policies";

	public LeavePolicyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_And_Get_Leave_Policy_By_Id()
	{
		var created = await CreateLeavePolicyAsync();

		var response = await Client.GetAsync($"{LeavePoliciesUrl}/{created.Id}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var detail = await response.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		detail.ShouldNotBeNull();
		detail.Id.ShouldBe(created.Id);
		detail.Rules.Count.ShouldBe(2);
		detail.Rules.ShouldContain(rule => rule.PaidType == "Paid" && rule.LeaveCount == 12);
		detail.Rules.ShouldContain(rule => rule.PaidType == "Unpaid" && rule.EntitlementMode == "Unlimited" && rule.IsUnlimited);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Accrual_Policy_Type()
	{
		var created = await CreateLeavePolicyAsync();
		var paidRule = created.Rules.Single(rule => rule.PaidType == "Paid");
		var unpaidRule = created.Rules.Single(rule => rule.PaidType == "Unpaid");

		var response = await Client.PutAsJsonAsync($"{LeavePoliciesUrl}/{created.Id}", new LeavePolicyUpdateRequest
		{
			PolicyTitle = created.PolicyTitle,
			Status = created.Status,
			EffectiveFrom = created.EffectiveFrom,
			EffectiveTo = created.EffectiveTo,
			Description = created.Description,
			Rules =
			[
				Rule(
					paidType: paidRule.PaidType,
					entitlementMode: paidRule.EntitlementMode,
					accrualPolicyType: "Yearly",
					leaveCount: paidRule.LeaveCount,
					carryForward: paidRule.CarryForward,
					encashmentAllowed: paidRule.EncashmentAllowed,
					encashmentTiming: paidRule.EncashmentTiming,
					allowNegativeBalance: paidRule.AllowNegativeBalance),
				Rule(
					paidType: unpaidRule.PaidType,
					entitlementMode: unpaidRule.EntitlementMode,
					leaveCount: unpaidRule.LeaveCount,
					allowNegativeBalance: unpaidRule.AllowNegativeBalance)
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await response.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.Rules.Count.ShouldBe(2);
		updated.Rules.Single(rule => rule.PaidType == "Paid").AccrualPolicyType.ShouldBe("Yearly");

		var detailResponse = await Client.GetAsync($"{LeavePoliciesUrl}/{created.Id}");
		detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var detail = await detailResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		detail.ShouldNotBeNull();
		detail.Rules.Single(rule => rule.PaidType == "Paid").AccrualPolicyType.ShouldBe("Yearly");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Leave_Policies_By_Search()
	{
		var marker = $"Policy-{Guid.NewGuid():N}";

		await CreateLeavePolicyAsync(marker);

		var response = await Client.GetAsync($"{LeavePoliciesUrl}?academicYearId={AcademicYearId}&page=1&pageSize=10&search={marker}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var list = await response.Content.ReadFromJsonAsync<LeavePolicyListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_List_Leave_Policies_With_Max_Page_Size()
	{
		var marker = $"Policy-{Guid.NewGuid():N}";

		await CreateLeavePolicyAsync(marker);

		var response = await Client.GetAsync($"{LeavePoliciesUrl}?academicYearId={AcademicYearId}&page=1&pageSize=500&search={marker}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var list = await response.Content.ReadFromJsonAsync<LeavePolicyListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.PageSize.ShouldBe(500);
		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Set_Default_Leave_Policy_And_Clear_Previous_Default()
	{
		var first = await CreateLeavePolicyAsync($"Default-A-{Guid.NewGuid():N}");
		var second = await CreateLeavePolicyAsync($"Default-B-{Guid.NewGuid():N}");

		var firstDefaultResponse = await Client.PatchAsync($"{LeavePoliciesUrl}/{first.Id}/set-default", null);
		firstDefaultResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var firstDefault = await firstDefaultResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		firstDefault.ShouldNotBeNull();
		firstDefault.IsDefault.ShouldBeTrue();

		var secondDefaultResponse = await Client.PatchAsync($"{LeavePoliciesUrl}/{second.Id}/set-default", null);
		secondDefaultResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var secondDefault = await secondDefaultResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		secondDefault.ShouldNotBeNull();
		secondDefault.IsDefault.ShouldBeTrue();

		var firstDetailResponse = await Client.GetAsync($"{LeavePoliciesUrl}/{first.Id}");
		firstDetailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var firstDetail = await firstDetailResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		firstDetail.ShouldNotBeNull();
		firstDetail.IsDefault.ShouldBeFalse();
	}

	private async Task<LeavePolicyResponse> CreateLeavePolicyAsync(string? policyTitle = null)
	{
		var request = new LeavePolicyCreateRequest
		{
			PolicyTitle = policyTitle ?? $"Policy-{Guid.NewGuid():N}",
			Status = "Active",
			EffectiveFrom = DateTime.UtcNow.Date,
			Description = "Policy for API testing",
			Rules = DefaultRules()
		};

		var response = await Client.PostAsJsonAsync($"{LeavePoliciesUrl}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave policy.");
	}

	private static List<LeavePolicyRuleRequest> DefaultRules() =>
	[
		Rule(
			leaveCount: 12,
			accrualPolicyType: "Monthly",
			carryForward: true,
			carryForwardLimit: 6,
			encashmentAllowed: true,
			encashmentTiming: "next_payroll"),
		Rule(
			paidType: "Unpaid",
			entitlementMode: "Unlimited",
			leaveCount: null,
			allowNegativeBalance: true)
	];

	private static LeavePolicyRuleRequest Rule(
		string paidType = "Paid",
		string entitlementMode = "Limited",
		string accrualPolicyType = "Yearly",
		int? leaveCount = 10,
		bool carryForward = false,
		int? carryForwardLimit = null,
		bool encashmentAllowed = false,
		string? encashmentTiming = null,
		bool allowNegativeBalance = false) =>
		new()
		{
			PaidType = paidType,
			EntitlementMode = entitlementMode,
			AccrualPolicyType = accrualPolicyType,
			LeaveCount = leaveCount,
			CarryForward = carryForward,
			CarryForwardLimit = carryForwardLimit,
			EncashmentAllowed = encashmentAllowed,
			EncashmentTiming = encashmentTiming,
			RequiresApproval = true,
			MinNoticeDays = 0,
			MaxDaysPerRequest = 30,
			AllowHalfDay = true,
			AllowNegativeBalance = allowNegativeBalance,
			IsActive = true
		};

	private Guid AcademicYearId => Factory.SeededAcademicYearId;
}
