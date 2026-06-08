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
public class ChargeCoreApi_Tests : ChargeApiTestsBase
{
	public ChargeCoreApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Charge_With_Approval_Required_Category_Returns_Pending_Status()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		var rawBody = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Charge creation failed. AcademicYearId={ChargeTestHelper.DefaultAcademicYearId}, StudentId={studentId}, CategoryId={categoryId}. Response: {rawBody}");
		var body = await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.ChargeId.ShouldNotBe(Guid.Empty);
		body.Data.Status.ShouldBe(0); // Pending
		body.Data.ResolvedStudentCount.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Charge_With_No_Approval_Category_Returns_Approved_Status_Immediately()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: false);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.Status.ShouldBe(1); // Approved
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Charge_Response_Has_All_Scope_Fields_Populated()
	{
		var (studentId, boardId) = await ChargeTestHelper.CreateStudentAsync(Client);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions);
		body.ShouldNotBeNull();
		var data = body.Data;
		data.ShouldNotBeNull();
		data.BoardId.ShouldBe(boardId);
		data.EducationMediumId.ShouldNotBe(Guid.Empty);
		data.ChargeName.ShouldNotBeNullOrWhiteSpace();
		data.ResolvedStudentCount.ShouldBe(1);
		data.CategoryName.ShouldNotBeNullOrWhiteSpace();
		data.RequestorId.ShouldNotBe(Guid.Empty);
		data.AcademicYearId.ShouldBe(ChargeTestHelper.DefaultAcademicYearId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_GetById_Return_Full_Detail_Shape()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{chargeId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeDetailResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		var data = body.Data;
		data.ShouldNotBeNull();
		data.ChargeId.ShouldBe(chargeId);
		data.Students.ShouldNotBeNull();
		data.Students.Count.ShouldBeGreaterThanOrEqualTo(1);
		data.Students[0].StudentId.ShouldNotBe(Guid.Empty);
		data.Audience.ShouldNotBeNull();
		data.Audience.Scope.BoardId.ShouldNotBe(Guid.Empty);
		data.Requestor.ShouldNotBeNull();
		data.Requestor.RequestorId.ShouldNotBe(Guid.Empty);
		data.ApprovalDetails.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_GetById_Nonexistent_Charge_Returns_Error()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_GetAll_Return_Paginated_List_With_Status_Counts()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}&page=1&pageSize=20");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ChargeListResponse>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
		body.CurrentPage.ShouldBe(1);
		body.PageSize.ShouldBe(20);
		body.Items.ShouldContain(i => i.ChargeId == chargeId);

		// Status counts must all be present (values may be zero for other statuses)
		(body.PendingCount + body.ApprovedCount + body.RejectedCount).ShouldBeGreaterThanOrEqualTo(1);
	}

	#endregion
}
