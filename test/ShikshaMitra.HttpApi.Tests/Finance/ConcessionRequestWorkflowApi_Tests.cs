using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionRequestWorkflowApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestWorkflowApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Approve_Pending_Request_With_DueFee_Allocations()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();

		var contextResp = await Client.GetAsync($"{BaseUrl}/{pendingId}/approval-context");
		contextResp.EnsureSuccessStatusCode();
		var context = await contextResp.Content.ReadFromJsonAsync<ConcessionApprovalContextDto>(JsonOptions);
		context.ShouldNotBeNull();

		var group = context!.FeeHeadGroups.First();
		var selectable = group.DueFeeLines.Where(l => l.IsSelectable).ToList();
		if (selectable.Count == 0)
		{
			return;
		}

		var perLine = group.RequestedConcessionAmount / selectable.Count;
		var approval = new ApproveConcessionRequestDto
		{
			Message = "Approved for tests",
			RequestConcurrencyStamp = context.RequestConcurrencyStamp,
			FeeHeadApprovals =
			[
				new FeeHeadApprovalDto
				{
					FeeHeadId = group.FeeHeadId,
					ApprovedConcessionAmount = group.RequestedConcessionAmount,
					DueFeeAllocations = selectable.Select(l => new DueFeeAllocationDto
					{
						DueFeeId = l.DueFeeId,
						ConcessionAmount = perLine,
						ConcurrencyStamp = l.ConcurrencyStamp,
					}).ToList(),
				},
			],
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/{pendingId}/approve", approval, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionApprovalResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		result!.Status.ShouldBe(ConcessionRequestStatus.Approved);
		result.StudentConcessionTypeRequestId.ShouldBe(pendingId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Pending_Request()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = "Rejected for tests" },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionRejectionResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		result!.Status.ShouldBe(ConcessionRequestStatus.Rejected);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Cancellation_After_Approval()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();
		var contextResp = await Client.GetAsync($"{BaseUrl}/{pendingId}/approval-context");
		contextResp.EnsureSuccessStatusCode();
		var context = await contextResp.Content.ReadFromJsonAsync<ConcessionApprovalContextDto>(JsonOptions);
		context.ShouldNotBeNull();

		var group = context!.FeeHeadGroups.First();
		var selectable = group.DueFeeLines.Where(line => line.IsSelectable).ToList();
		selectable.ShouldNotBeEmpty();
		var perLine = group.RequestedConcessionAmount / selectable.Count;

		var approveResp = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{pendingId}/approve",
			new ApproveConcessionRequestDto
			{
				Message = "Approve for cancel test",
				RequestConcurrencyStamp = context.RequestConcurrencyStamp,
				FeeHeadApprovals =
				[
					new FeeHeadApprovalDto
					{
						FeeHeadId = group.FeeHeadId,
						ApprovedConcessionAmount = group.RequestedConcessionAmount,
						DueFeeAllocations = selectable.Select(line => new DueFeeAllocationDto
						{
							DueFeeId = line.DueFeeId,
							ConcessionAmount = perLine,
							ConcurrencyStamp = line.ConcurrencyStamp,
						}).ToList(),
					},
				],
			},
			JsonOptions);
		approveResp.StatusCode.ShouldBe(HttpStatusCode.OK, await approveResp.Content.ReadAsStringAsync());

		var cancelResp = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);
		cancelResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await cancelResp.Content.ReadAsStringAsync())
			.ShouldContain(FinanceConcessionErrorCodes.CannotApproveOrRejectNonPending);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Cancel_Pending_Request()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();

		var cancelResp = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);

		cancelResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var cancelBody = await cancelResp.Content.ReadFromJsonAsync<ConcessionCancellationResponseDto>(JsonOptions);
		cancelBody.ShouldNotBeNull();
		cancelBody!.Status.ShouldBe(ConcessionRequestStatus.Cancelled);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Approval_When_Request_ConcurrencyStamp_Is_Stale()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();
		var contextResp = await Client.GetAsync($"{BaseUrl}/{pendingId}/approval-context");
		contextResp.EnsureSuccessStatusCode();
		var context = await contextResp.Content.ReadFromJsonAsync<ConcessionApprovalContextDto>(JsonOptions);
		context.ShouldNotBeNull();

		var group = context!.FeeHeadGroups.First();
		var line = group.DueFeeLines.First(candidate => candidate.IsSelectable);
		var approval = new ApproveConcessionRequestDto
		{
			Message = "Attempt with stale request state",
			RequestConcurrencyStamp = "stale-request-stamp",
			FeeHeadApprovals =
			[
				new FeeHeadApprovalDto
				{
					FeeHeadId = group.FeeHeadId,
					ApprovedConcessionAmount = Math.Min(group.RequestedConcessionAmount, line.OutstandingAmount),
					DueFeeAllocations =
					[
						new DueFeeAllocationDto
						{
							DueFeeId = line.DueFeeId,
							ConcessionAmount = Math.Min(group.RequestedConcessionAmount, line.OutstandingAmount),
							ConcurrencyStamp = line.ConcurrencyStamp,
						},
					],
				},
			],
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/{pendingId}/approve", approval, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain(FinanceConcessionErrorCodes.ConcurrencyConflict);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Approval_When_DueFee_ConcurrencyStamp_Is_Stale()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();
		var contextResp = await Client.GetAsync($"{BaseUrl}/{pendingId}/approval-context");
		contextResp.EnsureSuccessStatusCode();
		var context = await contextResp.Content.ReadFromJsonAsync<ConcessionApprovalContextDto>(JsonOptions);
		context.ShouldNotBeNull();

		var group = context!.FeeHeadGroups.First();
		var line = group.DueFeeLines.First(candidate => candidate.IsSelectable);
		var amount = Math.Min(group.RequestedConcessionAmount, line.OutstandingAmount);
		var approval = new ApproveConcessionRequestDto
		{
			Message = "Attempt with stale due-fee state",
			RequestConcurrencyStamp = context.RequestConcurrencyStamp,
			FeeHeadApprovals =
			[
				new FeeHeadApprovalDto
				{
					FeeHeadId = group.FeeHeadId,
					ApprovedConcessionAmount = amount,
					DueFeeAllocations =
					[
						new DueFeeAllocationDto
						{
							DueFeeId = line.DueFeeId,
							ConcessionAmount = amount,
							ConcurrencyStamp = "stale-due-fee-stamp",
						},
					],
				},
			],
		};

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/{pendingId}/approve", approval, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain(FinanceConcessionErrorCodes.ConcurrencyConflict);
	}
}
