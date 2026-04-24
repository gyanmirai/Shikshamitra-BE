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
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Shared test helper for Concession Request API tests.
/// Provides methods to create prerequisite domain objects via the API
/// and convenience factories for request DTOs.
/// </summary>
public static class ConcessionRequestTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private const string RegistrationUrl = "/api/student/enrollment/registrations";
	private const string ConcessionRequestUrl = "/api/concession-request";
	private const string FeeCategoryUrl = "/api/fee-categories";
	private const string FeeHeadUrl = "/api/fee-heads";

	// ── Static defaults set by the test class ctor from the factory ──────────

	public static Guid DefaultAcademicYearId { get; set; }
	public static Guid DefaultMediumId { get; set; }
	public static Guid DefaultStandardId { get; set; }
	public static Guid DefaultDivisionId { get; set; }

	// ── Prerequisite data builders ────────────────────────────────────────────

	/// <summary>
	/// Creates a student via direct registration with all four enrollment context
	/// fields populated. Returns the student ID and its derived BoardId.
	/// </summary>
	public static async Task<(Guid StudentId, Guid BoardId)> CreateStudentAsync(HttpClient client)
	{
		var dto = new CreateDirectRegistrationDto
		{
			AcademicMediumId = DefaultMediumId,
			StandardId = DefaultStandardId,
			DivisionId = DefaultDivisionId,
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
		response.EnsureSuccessStatusCode();

		var result = await response.Content
			.ReadFromJsonAsync<Result<StudentGeneralDetailsDto>>(JsonOptions)
			?? throw new InvalidOperationException("Null response from student creation");

		var details = result.Data
			?? throw new InvalidOperationException("Null student details in response");

		return (details.StudentId, details.AcademicBoardId);
	}

	/// <summary>
	/// Creates a ConcessionType for the seeded academic year and returns its ID.
	/// The type is created with no required documents for simplicity.
	/// </summary>
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
		createResponse.EnsureSuccessStatusCode();

		// Retrieve the created type by searching its unique name
		var listResponse = await client.GetAsync(
			$"/api/{DefaultAcademicYearId}/concession-types?searchTerm={name}&maxResultCount=50");
		listResponse.EnsureSuccessStatusCode();

		var list = await listResponse.Content
			.ReadFromJsonAsync<Result<PagedResultDto<ConcessionTypeResponse>>>(JsonOptions)
			?? throw new InvalidOperationException("Null concession type list response");

		return list.Data?.Items.FirstOrDefault(t => t.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created concession type '{name}'");
	}

	/// <summary>
	/// Creates a FeeCategory for the seeded academic year and returns its ID.
	/// </summary>
	public static async Task<Guid> CreateFeeCategoryAsync(HttpClient client)
	{
		var name = TestDataHelper.UniqueName("FeeCat");
		var request = new FeeCategoryCreateRequest { Name = name, Description = "Test fee category" };

		var createResponse = await client.PostAsJsonAsync(
			$"{FeeCategoryUrl}?academicYearId={DefaultAcademicYearId}", request, JsonOptions);
		createResponse.EnsureSuccessStatusCode();

		// Retrieve by name
		var listResponse = await client.GetAsync(
			$"{FeeCategoryUrl}?academicYearId={DefaultAcademicYearId}&search={name}&pageSize=50");
		listResponse.EnsureSuccessStatusCode();

		var list = await listResponse.Content
			.ReadFromJsonAsync<FeeCategoryListResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null fee category list response");

		return list.Items?.FirstOrDefault(c => c.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created fee category '{name}'");
	}

	/// <summary>
	/// Creates a FeeHead under the given category for the seeded academic year and returns its ID.
	/// </summary>
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
		createResponse.EnsureSuccessStatusCode();

		// Retrieve by name
		var listResponse = await client.GetAsync(
			$"{FeeHeadUrl}?academicYearId={DefaultAcademicYearId}&search={name}&pageSize=50");
		listResponse.EnsureSuccessStatusCode();

		var list = await listResponse.Content
			.ReadFromJsonAsync<FeeHeadListResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null fee head list response");

		return list.Items?.FirstOrDefault(h => h.Name == name)?.Id
			?? throw new InvalidOperationException($"Could not locate created fee head '{name}'");
	}

	/// <summary>
	/// Creates a ConcessionRequest (Draft) and returns the ID of the first created record,
	/// fetched by filtering the list by studentId.
	/// </summary>
	public static async Task<Guid> CreateConcessionRequestAsync(
		HttpClient client,
		Guid studentId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		IEnumerable<Guid> concessionTypeIds)
	{
		var input = new CreateConcessionRequestDto
		{
			StudentId = studentId,
			BoardId = boardId,
			EducationMediumId = educationMediumId,
			StandardId = standardId,
			DivisionId = divisionId,
			ConcessionTypeIds = concessionTypeIds.ToList(),
		};

		var createResponse = await client.PostAsJsonAsync(ConcessionRequestUrl, input, JsonOptions);
		createResponse.EnsureSuccessStatusCode();

		// Fetch the newly created request by studentId filter
		var listResponse = await client.GetAsync(
			$"{ConcessionRequestUrl}?studentId={studentId}&maxResultCount=50");
		listResponse.EnsureSuccessStatusCode();

		var list = await listResponse.Content
			.ReadFromJsonAsync<PagedResultDto<ConcessionRequestDto>>(JsonOptions)
			?? throw new InvalidOperationException("Null concession request list response");

		return list.Items?.FirstOrDefault()?.Id
			?? throw new InvalidOperationException("Could not find created concession request");
	}

	// ── DTO builders ──────────────────────────────────────────────────────────

	/// <summary>
	/// Builds a <see cref="CreateConcessionRequestDto"/> with all required fields.
	/// </summary>
	public static CreateConcessionRequestDto BuildCreateDto(
		Guid studentId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		Guid concessionTypeId)
	{
		return new CreateConcessionRequestDto
		{
			StudentId = studentId,
			BoardId = boardId,
			EducationMediumId = educationMediumId,
			StandardId = standardId,
			DivisionId = divisionId,
			ConcessionTypeIds = new List<Guid> { concessionTypeId },
		};
	}
}
