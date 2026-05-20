using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionRequestValidationApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestValidationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Handle_Request_When_No_Auth_Token()
	{
		using var anonClient = CreateAnonymousClient();
		var response = await anonClient.GetAsync($"{BaseUrl}?maxResultCount=10");
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Wrong_Board_Id()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		var input = ConcessionRequestTestHelper.BuildCreateDto(
			studentId,
			boardId: Guid.NewGuid(),
			educationMediumId: ConcessionRequestTestHelper.DefaultMediumId,
			standardId: ConcessionRequestTestHelper.DefaultStandardId,
			divisionId: ConcessionRequestTestHelper.DefaultDivisionId,
			concessionTypeId: typeId);

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Nonexistent_Student()
	{
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		var input = ConcessionRequestTestHelper.BuildCreateDto(
			studentId: Guid.NewGuid(),
			boardId: Guid.NewGuid(),
			educationMediumId: ConcessionRequestTestHelper.DefaultMediumId,
			standardId: ConcessionRequestTestHelper.DefaultStandardId,
			divisionId: ConcessionRequestTestHelper.DefaultDivisionId,
			concessionTypeId: typeId);

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Nonexistent_Concession_Type()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

		var input = ConcessionRequestTestHelper.BuildCreateDto(
			studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			concessionTypeId: Guid.NewGuid());

		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Submit_Non_Draft_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();
		var response = await Client.PostAsJsonAsync($"{BaseUrl}/{pendingId}/submit", (object?)null, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Approve_Non_Pending_Request()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var draftId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{draftId}/approve",
			new ApproveConcessionRequestDto { Message = "Cannot approve draft" },
			JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Delete_Non_Draft_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();
		var response = await Client.DeleteAsync($"{BaseUrl}/{pendingId}");
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Save_Fee_Heads_For_Non_Draft_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();
		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		var getResp = await Client.GetAsync($"{BaseUrl}/{pendingId}");
		getResp.EnsureSuccessStatusCode();
		var detail = (await getResp.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions))!.Data!;
		var typeId = detail.SelectedConcessionTypes[0].ConcessionTypeId;

		var input = new SaveFeeHeadsDataDto
		{
			FeeHeadsData = new List<ConcessionTypeFeeHeadsDto>
			{
				new() { ConcessionTypeId = typeId, SelectedFeeHeadIds = new List<Guid> { feeHeadId } },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{pendingId}/feeHeadsData", input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Value_Entry_Percentage_Over_100()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		(await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/feeHeadsData",
			new SaveFeeHeadsDataDto
			{
				FeeHeadsData = new List<ConcessionTypeFeeHeadsDto>
				{
					new() { ConcessionTypeId = typeId, SelectedFeeHeadIds = new List<Guid> { feeHeadId } },
				},
			}, JsonOptions)).EnsureSuccessStatusCode();

		var valueInput = new ValueEntryDto
		{
			ConcessionTypeValues = new List<ConcessionTypeValueBlockDto>
			{
				new()
				{
					ConcessionTypeId = typeId,
					FeeHeadValues = new List<FeeHeadValueEntryDto>
					{
						new() { FeeHeadId = feeHeadId, BasedOn = 1, Amount = 105m, AnnualBaseAmount = 5000m },
					},
				},
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/value-entry", valueInput, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Flat_Discount_Exceeds_Base_Amount()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		(await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/feeHeadsData",
			new SaveFeeHeadsDataDto
			{
				FeeHeadsData = new List<ConcessionTypeFeeHeadsDto>
				{
					new() { ConcessionTypeId = typeId, SelectedFeeHeadIds = new List<Guid> { feeHeadId } },
				},
			}, JsonOptions)).EnsureSuccessStatusCode();

		var valueInput = new ValueEntryDto
		{
			ConcessionTypeValues = new List<ConcessionTypeValueBlockDto>
			{
				new()
				{
					ConcessionTypeId = typeId,
					FeeHeadValues = new List<FeeHeadValueEntryDto>
					{
						new() { FeeHeadId = feeHeadId, BasedOn = 0, Amount = 99999m, AnnualBaseAmount = 5000m },
					},
				},
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/value-entry", valueInput, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Save_Value_Entry_Missing_Concession_Type()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var valueInput = new ValueEntryDto
		{
			ConcessionTypeValues = new List<ConcessionTypeValueBlockDto>
			{
				new() { ConcessionTypeId = Guid.NewGuid(), FeeHeadValues = new List<FeeHeadValueEntryDto>() },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/value-entry", valueInput, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Reject_Already_Rejected_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		(await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = "Initial rejection for test." },
			JsonOptions)).EnsureSuccessStatusCode();

		var secondAttempt = await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = "Second rejection should fail." },
			JsonOptions);

		secondAttempt.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Save_Document_Upload_Missing_Concession_Type()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var input = new UploadDocumentsDto
		{
			ConcessionTypeDocuments = new List<ConcessionTypeDocumentBlockDto>
			{
				new() { ConcessionTypeId = Guid.NewGuid(), Documents = new List<DocumentUploadEntryDto>() },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/document-upload", input, JsonOptions);
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Duplicate_Request_For_Same_Student_And_Type()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		var input = ConcessionRequestTestHelper.BuildCreateDto(
			studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			typeId);

		var first = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		first.EnsureSuccessStatusCode();

		var second = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		second.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Conflict, HttpStatusCode.InternalServerError);
	}
}
