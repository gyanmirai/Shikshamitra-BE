using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeWorkflowApi_Tests : ChargeApiTestsBase
{
	public ChargeWorkflowApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Approve_Pending_Charge_By_Different_User_Returns_Approved()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		using var approverClient = CreateClientAs(Guid.NewGuid());

		var response = await approverClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/approve",
			new ApproveChargeRequest { ApprovalNotes = "Approved in test" },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ApprovalResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.ChargeId.ShouldBe(chargeId);
		body.Data.Status.ShouldBe(1); // Approved
		body.Data.ApprovedBy.ShouldNotBe(Guid.Empty);
		body.Data.ApprovedOn.ShouldBeGreaterThan(default);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Pending_Charge_By_Different_User_Returns_Rejected_With_Reason()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		using var rejectorClient = CreateClientAs(Guid.NewGuid());
		const string reason = "Insufficient documentation for this charge.";

		var response = await rejectorClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/reject",
			new RejectChargeRequest { RejectionReason = reason },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<RejectionResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.ChargeId.ShouldBe(chargeId);
		body.Data.Status.ShouldBe(2); // Rejected
		body.Data.RejectedBy.ShouldNotBe(Guid.Empty);
		body.Data.RejectionReason.ShouldBe(reason);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Pending_Charge_Amount_Returns_200_With_Updated_Amount()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		const decimal newAmount = 999m;

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{chargeId}",
			new UpdateChargeRequest { Amount = newAmount },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.Amount.ShouldBe(newAmount);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Not_Update_Approved_Charge_Returns_Error()
	{
		var chargeId = await CreateAutoApprovedChargeAsync();

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{chargeId}",
			new UpdateChargeRequest { Amount = 100m },
			JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Pending_Charge_Returns_204_And_Subsequent_GetById_Fails()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{chargeId}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{chargeId}");
		getResponse.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Not_Delete_Approved_Charge_Returns_Error()
	{
		var chargeId = await CreateAutoApprovedChargeAsync();

		var response = await Client.DeleteAsync($"{BaseUrl}/{chargeId}");

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_GetAll_With_BoardId_Filter_Returns_Only_Matching_Charges()
	{
		var (_, boardId, _) = await CreatePendingChargeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}&boardId={boardId}&page=1&pageSize=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.Items.ShouldAllBe(i => i.BoardId == boardId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_GetAll_With_EducationMediumId_Filter_Returns_Matching_Charges()
	{
		var (_, _, mediumId) = await CreatePendingChargeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}&educationMediumId={mediumId}&page=1&pageSize=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.Items.ShouldAllBe(i => i.EducationMediumId == mediumId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_GetAll_With_PageSize_Over_100_Cap_At_100()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}&page=1&pageSize=200");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.PageSize.ShouldBe(100);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_GetAll_Page_2_Returns_Empty_When_Data_Is_Sparse()
	{
		// Create only one charge — page 2 of pageSize 50 must be empty
		await CreatePendingChargeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}&page=2&pageSize=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldBeEmpty();
	}

	#endregion
}
