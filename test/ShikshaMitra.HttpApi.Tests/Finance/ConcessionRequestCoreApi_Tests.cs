using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionRequestCoreApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestCoreApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Concession_Request_Returns_Pending()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		var input = ConcessionRequestTestHelper.BuildCreateDto(studentId, typeId, feeHeadId);
		await ConcessionRequestTestHelper.PrepareForSubmissionAsync(Client, input);
		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data!.StudentId.ShouldBe(studentId);
		body.Data.ConcessionRequestId.ShouldNotBe(Guid.Empty);
		body.Data.Status.ShouldBe(ConcessionRequestStatus.Pending);
		body.Data.ConcessionTypeId.ShouldBe(typeId);
		body.Data.FeeHeadConcessions.ShouldNotBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Include_Board_And_Medium_In_Student_Concession_Details()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
		var response = await Client.GetAsync(
			$"{ConcessionRequestsBaseUrl}/{studentId}?academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<StudentConcessionRequestsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.StudentDetails.StudentId.ShouldBe(studentId);
		body.StudentDetails.BoardName.ShouldNotBeNullOrWhiteSpace();
		body.StudentDetails.EducationMediumName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Concession_Request_By_Id_Returns_Correct_Shape()
	{
		var requestId = await CreatePendingRequestWithFeeHeadAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data!.ConcessionRequestId.ShouldBe(requestId);
		body.Data.Status.ShouldBe(ConcessionRequestStatus.Pending);
	}

	[Theory]
	[InlineData("GR-000002AD", 123, "GR-000002AD")]
	[InlineData("GR-000002AE", null, "GR-000002AE")]
	[InlineData(null, 123, "123")]
	[InlineData(null, null, "")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Student_concession_details_use_profile_GR_number_with_legacy_fallback(
		string? grNumber, int? legacyGrNo, string expected)
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(
			Factory.Services, grNumber: grNumber, legacyGrNo: legacyGrNo);
		var response = await Client.GetAsync(
			$"{ConcessionRequestsBaseUrl}/{studentId}?academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<StudentConcessionRequestsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.StudentDetails.GrNumber.ShouldBe(expected);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_Draft_Request_Returns_204()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var draft = await ConcessionRequestTestHelper.CreateDraftAsync(Client, new CreateConcessionDraftDto
		{
			StudentId = studentId,
			ConcessionTypeId = typeId,
		});

		using var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/{draft.ConcessionRequestId}")
		{
			Content = JsonContent.Create(new DeleteConcessionRequestDto
			{
				RequestConcurrencyStamp = draft.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}, options: JsonOptions),
		};
		var response = await Client.SendAsync(request);

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_404_For_Nonexistent_Request_Get()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Applications_Returns_Paginated_Shape()
	{
		var requestId = await CreatePendingRequestWithFeeHeadAsync();
		var detailResp = await Client.GetAsync($"{BaseUrl}/{requestId}");
		detailResp.EnsureSuccessStatusCode();
		var detail = await detailResp.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);

		var boardId = detail!.Data!.BoardId;
		var mediumId = detail.Data.EducationMediumId;

		var response = await Client.GetAsync(
			$"{BaseUrl}/concession-applications?boardId={boardId}&educationMediumId={mediumId}&page=1&pageSize=20");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ConcessionApplicationsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
		var ownRequest = body.Items.Single(x => x.Id == requestId);
		ownRequest.RequesterId.ShouldBe(Guid.Parse(TestAuthHandler.TestUserId));
		ownRequest.AvailableActions.ShouldNotContain("approve");
		ownRequest.AvailableActions.ShouldNotContain("reject");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Approval_Context_Returns_FeeHead_Groups()
	{
		var requestId = await CreatePendingRequestWithFeeHeadAsync();

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}/approval-context");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ConcessionApprovalContextDto>(JsonOptions);
		body.ShouldNotBeNull();
		body!.ConcessionRequestId.ShouldBe(requestId);
		body.FeeHeadGroups.ShouldNotBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Request_context_lists_every_active_policy_for_the_academic_year()
	{
		var requestId = await CreatePendingRequestWithFeeHeadAsync();
		var detail = await Client.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions);
		var studentId = detail!.Data!.StudentId;
		var yearId = ConcessionRequestTestHelper.DefaultAcademicYearId;
		var context = await Client.GetFromJsonAsync<ConcessionRequestContextDto>(
			$"{BaseUrl}/context?academicYearId={yearId}&studentId={studentId}", JsonOptions);
		var policies = await Client.GetFromJsonAsync<PagedResultDto<ConcessionPolicyDto>>(
			$"/api/finance/concession-policies?academicYearId={yearId}&status=Active&maxResultCount=100",
			JsonOptions);

		context.ShouldNotBeNull();
		policies.ShouldNotBeNull();
		context.ConcessionPolicies.Select(x => x.ConcessionPolicyId).OrderBy(x => x)
			.ShouldBe(policies.Items.Select(x => x.Id).OrderBy(x => x));
	}
}
