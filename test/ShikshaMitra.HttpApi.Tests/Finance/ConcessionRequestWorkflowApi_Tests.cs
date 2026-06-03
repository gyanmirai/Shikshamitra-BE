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
	public async Task Should_Cancel_Approved_Request()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();

		await Client.PostAsJsonAsync(
			$"{BaseUrl}/{pendingId}/approve",
			new ApproveConcessionRequestDto { Message = "Auto-approve for cancel test" },
			JsonOptions);

		var cancelResp = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);
		cancelResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var cancelBody = await cancelResp.Content.ReadFromJsonAsync<ConcessionCancellationResponseDto>(JsonOptions);
		cancelBody.ShouldNotBeNull();
		cancelBody!.Status.ShouldBe(ConcessionRequestStatus.Cancelled);
	}
}
