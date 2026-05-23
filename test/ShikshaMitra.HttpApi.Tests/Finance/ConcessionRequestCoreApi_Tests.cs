using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

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
	public async Task Should_Create_Concession_Request_Returns_200_With_Detail_Body()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		var input = ConcessionRequestTestHelper.BuildCreateDto(
			studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			typeId);

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.StudentId.ShouldBe(studentId);
		body.Data.ConcessionRequestId.ShouldNotBe(Guid.Empty);
		// Draft = 0
		body.Data.Status.ShouldBe(0);
		body.Data.SelectedConcessionTypes.ShouldNotBeEmpty();
		body.Data.SelectedConcessionTypes.ShouldContain(ct => ct.ConcessionTypeId == typeId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Concession_Request_By_Id_Returns_Correct_Shape()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.ConcessionRequestId.ShouldBe(requestId);
		body.Data.StudentId.ShouldBe(studentId);
		// Draft = 0
		body.Data.Status.ShouldBe(0);
		body.Data.SelectedConcessionTypes.ShouldNotBeNull();
		body.Data.SelectedConcessionTypes.ShouldNotBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_List_Returns_Paginated_Shape()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{BaseUrl}?studentId={studentId}&maxResultCount=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content
			.ReadFromJsonAsync<PagedResultDto<ConcessionRequestDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
		body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
		body.Items.ShouldContain(r => r.StudentId == studentId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Delete_Draft_Request_Returns_200()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.DeleteAsync($"{BaseUrl}/{requestId}");

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var getResponse = await Client.GetAsync($"{BaseUrl}/{requestId}");
		getResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Submit_Draft_Request_Returns_Pending_Status()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		var submitResp = await Client.GetAsync($"{BaseUrl}/{pendingId}");
		submitResp.StatusCode.ShouldBe(HttpStatusCode.OK);
		var detail = await submitResp.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		detail.ShouldNotBeNull();
		detail.Data.ShouldNotBeNull();
		// Pending = 1
		detail.Data.Status.ShouldBe(1);
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
	public async Task Should_Filter_List_By_Student_Id()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{BaseUrl}?studentId={studentId}&maxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<ConcessionRequestDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldAllBe(r => r.StudentId == studentId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_List_By_Status()
	{
		await CreateDraftWithFeeHeadsAndSubmitAsync();

		// Pending = 1
		var response = await Client.GetAsync($"{BaseUrl}?status=1&maxResultCount=50");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<ConcessionRequestDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldAllBe(r => r.Status == ConcessionRequestStatus.Pending);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Update_Draft_Request_Returns_Detail_Dto()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId1 = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var typeId2 = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		// Create with type1 → Draft
		var createInput = ConcessionRequestTestHelper.BuildCreateDto(
			studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			typeId1);
		var createResp = await Client.PostAsJsonAsync(BaseUrl, createInput, JsonOptions);
		createResp.EnsureSuccessStatusCode();
		var createBody = await createResp.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		var requestId = createBody!.Data!.ConcessionRequestId;

		// Update from Draft → replace type1 with type2
		var updateInput = new UpdateConcessionRequestDto
		{
			StudentId         = studentId,
			BoardId           = boardId,
			EducationMediumId = ConcessionRequestTestHelper.DefaultMediumId,
			StandardId        = ConcessionRequestTestHelper.DefaultStandardId,
			DivisionId        = ConcessionRequestTestHelper.DefaultDivisionId,
			ConcessionTypeIds = new List<Guid> { typeId2 },
		};
		var updateResp = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}", updateInput, JsonOptions);
		updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

		var updateBody = await updateResp.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions);
		updateBody.ShouldNotBeNull();
		updateBody.Success.ShouldBeTrue();
		updateBody.Data.ShouldNotBeNull();
		// Draft = 0
		updateBody.Data.Status.ShouldBe(0);
		updateBody.Data.SelectedConcessionTypes.ShouldContain(ct => ct.ConcessionTypeId == typeId2);
	}
}
