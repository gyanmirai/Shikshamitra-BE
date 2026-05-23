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
public class ConcessionRequestWorkflowApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestWorkflowApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Approve_Pending_Request_Returns_Approved_Status()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		var response = await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/approve",
			new ApproveConcessionRequestDto { Message = "Approved for tests" },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionApprovalResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.StudentConcessionTypeRequestId.ShouldBe(pendingId);
		// Approved = 2
		result.Status.ShouldBe(2);
		result.StatusName.ShouldBe("Approved");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Pending_Request_Returns_Rejected_With_Reason()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();
		const string reason = "Supporting documents are incomplete.";

		var response = await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = reason },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionRejectionResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.StudentConcessionTypeRequestId.ShouldBe(pendingId);
		// Rejected = 3
		result.Status.ShouldBe(3);
		result.RejectionReason.ShouldBe(reason);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Save_Fee_Heads_Data_Returns_200()
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

		var input = new SaveFeeHeadsDataDto
		{
			FeeHeadsData = new List<ConcessionTypeFeeHeadsDto>
			{
				new() { ConcessionTypeId = typeId, SelectedFeeHeadIds = new List<Guid> { feeHeadId } },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/feeHeadsData", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Fee_Heads_Returns_Correct_Shape()
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

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}/fee-heads");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.StudentId.ShouldBe(studentId);
		body.Data.ConcessionTypes.ShouldNotBeEmpty();
		body.Data.ConcessionTypes[0].FeeHeadIds.ShouldContain(feeHeadId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Save_Value_Entry_Returns_200()
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
						new()
						{
							FeeHeadId = feeHeadId,
							BasedOn = 0,
							Amount = 500m,
							AnnualBaseAmount = 10000m,
						},
					},
				},
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/value-entry", valueInput, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Value_Entries_Returns_Correct_Shape()
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

		(await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/value-entry",
			new ValueEntryDto
			{
				ConcessionTypeValues = new List<ConcessionTypeValueBlockDto>
				{
					new()
					{
						ConcessionTypeId = typeId,
						FeeHeadValues = new List<FeeHeadValueEntryDto>
						{
							new()
							{
								FeeHeadId = feeHeadId,
								BasedOn = 1,
								Amount = 10m,
								AnnualBaseAmount = 5000m,
							},
						},
					},
				},
			}, JsonOptions)).EnsureSuccessStatusCode();

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}/value-entries");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestValueEntriesDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.StudentId.ShouldBe(studentId);
		body.Data.ConcessionTypeValues.ShouldNotBeEmpty();

		var saved = body.Data.ConcessionTypeValues[0].FeeHeadValues[0];
		saved.FeeHeadId.ShouldBe(feeHeadId);
		saved.BasedOn.ShouldBe(1);
		saved.Amount.ShouldBe(10m);
		saved.NetAmount.ShouldBe(4500m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Save_Document_Upload_Returns_200()
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
				new() { ConcessionTypeId = typeId, Documents = new List<DocumentUploadEntryDto>() },
			},
		};

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/document-upload", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Document_Uploads_Returns_Correct_Shape()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{BaseUrl}/{requestId}/document-upload");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDocumentsDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Success.ShouldBeTrue();
		body.Data.ShouldNotBeNull();
		body.Data.StudentId.ShouldBe(studentId);
		body.Data.ConcessionTypeDocuments.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Save_Description_Returns_200()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var input = new SaveDescriptionDto { Description = "This is a test description for the concession request." };

		var response = await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/description", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Student_Concession_Requests_Returns_200()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{ConcessionRequestsBaseUrl}/{studentId}?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<StudentConcessionRequestsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.StudentDetails.ShouldNotBeNull();
		body.StudentDetails.StudentId.ShouldBe(studentId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Concession_Applications_Returns_200()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);

		await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync(
			$"{BaseUrl}/concession-applications?boardId={boardId}&educationMediumId={ConcessionRequestTestHelper.DefaultMediumId}&page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ConcessionApplicationsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Concession_Documents_Flat_View_Returns_200()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.GetAsync($"{ConcessionRequestsBaseUrl}/{requestId}/documents?page=1&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<ConcessionDocumentsDto>(JsonOptions);
		body.ShouldNotBeNull();
		body.StudentConcessionTypeRequestId.ShouldBe(requestId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Cancel_Pending_Request_Returns_Cancelled_Status()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		var response = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionCancellationResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.StudentConcessionTypeRequestId.ShouldBe(pendingId);
		// Cancelled = 4
		result.Status.ShouldBe(4);
		result.StatusName.ShouldBe("Cancelled");
		result.CancelledAt.ShouldBeGreaterThan(default);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Cancel_Draft_Request_Returns_Cancelled_Status()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var draftId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var response = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{draftId}/cancel", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConcessionCancellationResponseDto>(JsonOptions);
		result.ShouldNotBeNull();
		// Cancelled = 4
		result.Status.ShouldBe(4);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_Cancel_Already_Approved_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		// Approve it first
		(await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/approve",
			new ApproveConcessionRequestDto { Message = "Auto-approve for cancel edge case test" },
			JsonOptions)).EnsureSuccessStatusCode();

		// Attempt cancel on an Approved request
		var response = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_Cancel_Already_Rejected_Request()
	{
		var pendingId = await CreateDraftWithFeeHeadsAndSubmitAsync();

		// Reject it first
		(await Client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = "Reject for cancel edge case test" },
			JsonOptions)).EnsureSuccessStatusCode();

		// Attempt cancel on a Rejected request
		var response = await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{pendingId}/cancel", null);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Allow_New_Request_After_Cancel()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var draftId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		// Cancel the draft request
		(await Client.PostAsync($"{ConcessionRequestsBaseUrl}/{draftId}/cancel", null)).EnsureSuccessStatusCode();

		// Now creating a new request for the same student + type should succeed
		var createInput = ConcessionRequestTestHelper.BuildCreateDto(
			studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			typeId);
		var response = await Client.PostAsJsonAsync(BaseUrl, createInput, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
