using System;
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
	private const string LeaveTypesUrl = "/api/hr-leave-types";
	private const string LeavePoliciesUrl = "/api/leave-policies";

	public LeavePolicyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_And_Get_Leave_Policy_By_Id()
	{
		var leaveType = await CreateLeaveTypeAsync();
		var created = await CreateLeavePolicyAsync(leaveType.Id);

		var response = await Client.GetAsync($"{LeavePoliciesUrl}/{created.Id}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var detail = await response.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		detail.ShouldNotBeNull();
		detail.Id.ShouldBe(created.Id);
		detail.Rules.ShouldHaveSingleItem();
		detail.Rules[0].LeaveType.Id.ShouldBe(leaveType.Id);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Accrual_Policy_Type()
	{
		var leaveType = await CreateLeaveTypeAsync();
		var created = await CreateLeavePolicyAsync(leaveType.Id);

		var response = await Client.PutAsJsonAsync($"{LeavePoliciesUrl}/{created.Id}", new LeavePolicyUpdateRequest
		{
			PolicyTitle = created.PolicyTitle,
			Status = created.Status,
			EffectiveFrom = created.EffectiveFrom,
			EffectiveTo = created.EffectiveTo,
			Description = created.Description,
			Rules =
			[
				new LeavePolicyRuleRequest
				{
					LeaveTypeId = leaveType.Id,
					AccrualPolicyType = "Yearly",
					LeaveCount = created.Rules[0].LeaveCount,
					PaidType = created.Rules[0].PaidType,
					CarryForward = created.Rules[0].CarryForward,
					EncashmentAllowed = created.Rules[0].EncashmentAllowed,
					EncashmentTiming = created.Rules[0].EncashmentTiming,
					RequiresApproval = created.Rules[0].RequiresApproval
				}
			]
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await response.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		updated.ShouldNotBeNull();
		updated.Rules.ShouldHaveSingleItem();
		updated.Rules[0].AccrualPolicyType.ShouldBe("Yearly");

		var detailResponse = await Client.GetAsync($"{LeavePoliciesUrl}/{created.Id}");
		detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var detail = await detailResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions);
		detail.ShouldNotBeNull();
		detail.Rules.ShouldHaveSingleItem();
		detail.Rules[0].AccrualPolicyType.ShouldBe("Yearly");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Leave_Policies_By_Search()
	{
		var leaveType = await CreateLeaveTypeAsync();
		var marker = $"Policy-{Guid.NewGuid():N}";

		await CreateLeavePolicyAsync(leaveType.Id, marker);

		var response = await Client.GetAsync($"{LeavePoliciesUrl}?academicYearId={AcademicYearId}&page=1&pageSize=10&search={marker}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var list = await response.Content.ReadFromJsonAsync<LeavePolicyListResponse>(JsonOptions);
		list.ShouldNotBeNull();
		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	private async Task<LeaveTypeResponse> CreateLeaveTypeAsync()
	{
		var request = new LeaveTypeCreateRequest
		{
			Name = $"LT-{Guid.NewGuid():N}",
			Color = "#1A73E8"
		};

		var response = await Client.PostAsJsonAsync($"{LeaveTypesUrl}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<LeaveTypeResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave type.");
	}

	private async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid leaveTypeId, string? policyTitle = null)
	{
		var request = new LeavePolicyCreateRequest
		{
			PolicyTitle = policyTitle ?? $"Policy-{Guid.NewGuid():N}",
			Status = "Active",
			EffectiveFrom = DateTime.UtcNow.Date,
			Description = "Policy for API testing",
			Rules =
			[
				new LeavePolicyRuleRequest
				{
					LeaveTypeId = leaveTypeId,
					AccrualPolicyType = "Monthly",
					LeaveCount = 12,
					PaidType = "Paid",
					CarryForward = true,
					EncashmentAllowed = true,
					EncashmentTiming = "next_payroll",
					RequiresApproval = true
				}
			]
		};

		var response = await Client.PostAsJsonAsync($"{LeavePoliciesUrl}?academicYearId={AcademicYearId}", request, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create leave policy.");
	}

	private Guid AcademicYearId => Factory.SeededAcademicYearId;
}
