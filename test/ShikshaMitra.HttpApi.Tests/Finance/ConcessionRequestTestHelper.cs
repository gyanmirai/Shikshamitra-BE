using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Students;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Shared test helper for Concession Request API tests.
/// </summary>
public static class ConcessionRequestTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private const string RegistrationUrl = "/api/student/enrollment/registrations";
	private const string ConcessionRequestUrl = "/api/concession-request";
	private const string FeeCategoryUrl = "/api/fee-categories";
	private const string FeeHeadUrl = "/api/fee-heads";

	public static Guid DefaultAcademicYearId { get; set; }
	public static Guid DefaultMediumId { get; set; }
	public static Guid DefaultStandardId { get; set; }
	public static Guid DefaultDivisionId { get; set; }

	public static async Task<(Guid StudentId, Guid BoardId)> CreateStudentAsync(
		HttpClient client,
		Guid? divisionId = null)
	{
		var dto = new CreateDirectRegistrationDto
		{
			AcademicMediumId = DefaultMediumId,
			StandardId = DefaultStandardId,
			DivisionId = divisionId ?? DefaultDivisionId,
			FormNumber = $"FORM-{Guid.NewGuid():N}"[..13].ToUpperInvariant(),
			FirstName = TestDataHelper.UniqueName("ConcStudent"),
			LastName = "TestStudent",
			DateOfBirth = DateTimeOffset.UtcNow.AddYears(-12),
			Gender = GenderEnum.Male,
			PrimaryContactNo = TestDataHelper.UniquePhone(),
			FamilyCast = "General",
			City = "Mumbai",
			State = new State("Maharashtra", "MH"),
			Country = new Country("India", "IN"),
		};

		var response = await client.PostAsJsonAsync($"{RegistrationUrl}/general-details", dto, JsonOptions);
		await EnsureSuccessAsync(response, "Student creation failed");

		var result = await response.Content
			.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions)
			?? throw new InvalidOperationException("Null response from student creation");

		var details = result.Data
			?? throw new InvalidOperationException("Null student details in response");

		return (details.StudentId, details.AcademicBoardId);
	}

	public static async Task<Guid> CreateConcessionTypeAsync(HttpClient client, string? nameSuffix = null)
	{
		var name = TestDataHelper.UniqueName(nameSuffix ?? "ConcType");

		var request = new ConcessionTypeRequest
		{
			Name = name,
			Documents = new List<ConcessionTypeDocumentDto>(),
			Description = "Test concession type",
		};

		var createResponse = await client.PostAsJsonAsync(
			$"/api/{DefaultAcademicYearId}/concession-types", request, JsonOptions);
		await EnsureSuccessAsync(createResponse, "Concession type creation failed");

		var listResponse = await client.GetAsync(
			$"/api/{DefaultAcademicYearId}/concession-types?searchTerm={name}&maxResultCount=50");
		await EnsureSuccessAsync(listResponse, "Concession type lookup failed");

		var list = await listResponse.Content
			.ReadFromJsonAsync<Result<PagedResultDto<ConcessionTypeResponse>>>(JsonOptions)
			?? throw new InvalidOperationException("Null concession type list response");

		return list.Data?.Items.FirstOrDefault(t => t.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created concession type '{name}'");
	}

	public static async Task<Guid> CreateFeeCategoryAsync(HttpClient client)
	{
		var name = TestDataHelper.UniqueName("FeeCat");
		var request = new FeeCategoryCreateRequest
		{
			Name = name,
			Description = "Test fee category",
			FeeHeads = new List<FeeHeadItemRequest>
			{
				new()
				{
					Name = TestDataHelper.UniqueName("Head"),
					Module = ShikshaMitra.Finance.Fees.Settings.FeeHeadModule.Finance,
					IsConcessionApplied = false,
					Description = "Seed fee head"
				}
			}
		};

		var createResponse = await client.PostAsJsonAsync(
			$"{FeeCategoryUrl}?academicYearId={DefaultAcademicYearId}", request, JsonOptions);
		await EnsureSuccessAsync(createResponse, "Fee category creation failed");

		var listResponse = await client.GetAsync(
			$"{FeeCategoryUrl}?academicYearId={DefaultAcademicYearId}&search={name}&pageSize=50");
		await EnsureSuccessAsync(listResponse, "Fee category lookup failed");

		var list = await listResponse.Content
			.ReadFromJsonAsync<FeeCategoryListResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null fee category list response");

		return list.Items?.FirstOrDefault(c => c.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created fee category '{name}'");
	}

	public static async Task<Guid> CreateFeeHeadAsync(HttpClient client, Guid feeCategoryId)
	{
		var name = TestDataHelper.UniqueName("FeeHead");
		var request = new FeeHeadCreateRequest
		{
			FeeCategoryId = feeCategoryId,
			Name = name,
			Description = "Test fee head",
		};

		var createResponse = await client.PostAsJsonAsync(
			$"{FeeHeadUrl}?academicYearId={DefaultAcademicYearId}", request, JsonOptions);
		await EnsureSuccessAsync(createResponse, "Fee head creation failed");

		var listResponse = await client.GetAsync(
			$"{FeeHeadUrl}?academicYearId={DefaultAcademicYearId}&search={name}&pageSize=50");
		await EnsureSuccessAsync(listResponse, "Fee head lookup failed");

		var list = await listResponse.Content
			.ReadFromJsonAsync<FeeHeadListResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null fee head list response");

		return list.Items?.FirstOrDefault(h => h.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created fee head '{name}'");
	}

	/// <summary>
	/// Creates a pending concession request (atomic create) and returns its id.
	/// </summary>
	public static async Task<Guid> CreateConcessionRequestAsync(
		HttpClient client,
		Guid studentId,
		Guid concessionTypeId,
		IReadOnlyList<Guid>? feeHeadIds = null)
	{
		var feeHeadConcessions = new List<FeeHeadConcessionDto>();
		if (feeHeadIds is { Count: > 0 })
		{
			foreach (var feeHeadId in feeHeadIds)
			{
				feeHeadConcessions.Add(new FeeHeadConcessionDto
				{
					FeeHeadId = feeHeadId,
					BasedOn = 0,
					Amount = 1000m,
				});
			}
		}

		var input = new CreateConcessionRequestDto
		{
			StudentId = studentId,
			ConcessionTypeId = concessionTypeId,
			Remarks = "Test concession request",
			FeeHeadConcessions = feeHeadConcessions,
		};
		await PrepareForSubmissionAsync(client, input);

		var createResponse = await client.PostAsJsonAsync(
			$"{ConcessionRequestUrl}?academicYearId={DefaultAcademicYearId}", input, JsonOptions);
		await EnsureSuccessAsync(createResponse, "Concession request creation failed");

		var body = await createResponse.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions)
			?? throw new InvalidOperationException("Null response body from concession request create");

		return body.Data?.ConcessionRequestId
			?? throw new InvalidOperationException("Could not find concessionRequestId in create response");
	}

	public static CreateConcessionRequestDto BuildCreateDto(Guid studentId, Guid concessionTypeId, Guid feeHeadId)
	{
		return new CreateConcessionRequestDto
		{
			StudentId = studentId,
			ConcessionTypeId = concessionTypeId,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			Remarks = "Test",
			FeeHeadConcessions =
			[
				new FeeHeadConcessionDto
				{
					FeeHeadId = feeHeadId,
					BasedOn = 0,
					Amount = 1000m,
				},
			],
		};
	}

	public static async Task PrepareForSubmissionAsync(
		HttpClient client,
		CreateConcessionRequestDto input)
	{
		var contextUrl = $"{ConcessionRequestUrl}/context?studentId={input.StudentId}" +
			$"&academicYearId={DefaultAcademicYearId}" +
			(input.StudentFeeStructureId.HasValue
				? $"&studentFeeStructureId={input.StudentFeeStructureId.Value}" : string.Empty);
		var context = await client.GetFromJsonAsync<ConcessionRequestContextDto>(contextUrl, JsonOptions)
			?? throw new InvalidOperationException("Null concession request context");
		var policy = context.ConcessionPolicies.FirstOrDefault(x => x.IsDefault)
			?? context.ConcessionPolicies.FirstOrDefault()
			?? throw new InvalidOperationException("No active concession policy available for the request");
		input.ConcessionPolicyId = policy.ConcessionPolicyId;
		input.ConcessionPolicyVersionId = policy.ConcessionPolicyVersionId;
		var previewResponse = await client.PostAsJsonAsync(
			$"{ConcessionRequestUrl}/preview?academicYearId={DefaultAcademicYearId}",
			new ConcessionRequestPreviewInputDto
			{
				StudentId = input.StudentId,
				StudentFeeStructureId = input.StudentFeeStructureId,
				ConcessionPolicyId = input.ConcessionPolicyId,
				ConcessionPolicyVersionId = input.ConcessionPolicyVersionId,
				ConcessionTypeId = input.ConcessionTypeId,
				FeeHeadConcessions = input.FeeHeadConcessions,
			},
			JsonOptions);
		await EnsureSuccessAsync(previewResponse, "Concession request preview failed");
		var preview = await previewResponse.Content
			.ReadFromJsonAsync<ConcessionRequestPreviewDto>(JsonOptions)
			?? throw new InvalidOperationException("Null concession request preview");
		input.StudentFeeStructureId = preview.StudentFeeStructureId;
		input.PreviewFingerprint = preview.PreviewFingerprint;
		input.IdempotencyKey ??= Guid.NewGuid().ToString("N");
	}

	public static async Task<ConcessionRequestDetailDto> CreateDraftAsync(
		HttpClient client,
		CreateConcessionDraftDto input)
	{
		if (input.ConcessionPolicyId == Guid.Empty || input.ConcessionPolicyVersionId == Guid.Empty)
		{
			var contextUrl = $"{ConcessionRequestUrl}/context?studentId={input.StudentId}" +
				$"&academicYearId={DefaultAcademicYearId}" +
				(input.StudentFeeStructureId.HasValue
					? $"&studentFeeStructureId={input.StudentFeeStructureId.Value}" : string.Empty);
			var context = await client.GetFromJsonAsync<ConcessionRequestContextDto>(contextUrl, JsonOptions)
				?? throw new InvalidOperationException("Null concession request context");
			var policy = context.ConcessionPolicies.FirstOrDefault(x => x.IsDefault)
				?? context.ConcessionPolicies.FirstOrDefault()
				?? throw new InvalidOperationException("No active concession policy available for the request");
			input.ConcessionPolicyId = policy.ConcessionPolicyId;
			input.ConcessionPolicyVersionId = policy.ConcessionPolicyVersionId;
		}
		input.IdempotencyKey ??= Guid.NewGuid().ToString("N");
		var response = await client.PostAsJsonAsync(
			$"{ConcessionRequestUrl}/drafts?academicYearId={DefaultAcademicYearId}", input, JsonOptions);
		await EnsureSuccessAsync(response, "Concession request draft creation failed");
		var body = await response.Content
			.ReadFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(JsonOptions)
			?? throw new InvalidOperationException("Null response body from concession request draft create");
		return body.Data ?? throw new InvalidOperationException("Null draft detail");
	}

	private static async Task EnsureSuccessAsync(HttpResponseMessage response, string context)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var body = await response.Content.ReadAsStringAsync();
		var snippet = body.Length > 1000 ? body[..1000] : body;
		throw new InvalidOperationException($"{context} ({(int)response.StatusCode}): {snippet}");
	}
}
