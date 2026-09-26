using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Charges;

/// <summary>
/// Shared test helper for Charge API tests.
/// Provides factory methods to create prerequisite domain objects through the API
/// and convenience builders for request DTOs.
/// </summary>
public static class ChargeTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private const string RegistrationUrl = "/api/student/enrollment/registrations";
	private const string ChargeCategoriesUrl = "/api/charge-categories";
	private const string ChargesUrl = "/api/app/charges";

	// ── Static defaults set by the test class constructor from the factory ────

	public static Guid DefaultAcademicYearId { get; set; }
	public static Guid DefaultMediumId { get; set; }
	public static Guid DefaultStandardId { get; set; }
	public static Guid DefaultDivisionId { get; set; }

	// ── Prerequisite data factory methods ────────────────────────────────────

	/// <summary>
	/// Creates a student via direct registration with all four enrollment scope fields
	/// populated so the student satisfies the <c>ChargeManager</c> scope guard.
	/// Returns the student ID and derived board ID.
	/// </summary>
	public static async Task<(Guid StudentId, Guid BoardId)> CreateStudentAsync(
		HttpClient client,
		ShikshaMitraApiTestFactory factory,
		bool assignFeeStructure = true)
	{
		var dto = new CreateDirectRegistrationDto
		{
			AcademicMediumId = DefaultMediumId,
			StandardId = DefaultStandardId,
			DivisionId = DefaultDivisionId,
			FormNumber = $"CHRG-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
			FirstName = TestDataHelper.UniqueName("ChrgStudent"),
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

		if (assignFeeStructure)
		{
			await AddFeeStructureAssignmentAsync(details.StudentId, factory);
		}

		return (details.StudentId, details.AcademicBoardId);
	}

	private static async Task AddFeeStructureAssignmentAsync(
		Guid studentId,
		ShikshaMitraApiTestFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var provider = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (provider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var structures = await provider.GetRequiredService<IRepository<FeeStructure, Guid>>()
				.GetListAsync(structure => structure.StandardId == DefaultStandardId);
			var structure = structures.FirstOrDefault()
				?? throw new InvalidOperationException("Charge API tests require a seeded fee structure for the default standard.");
			await provider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(
				new StudentFeeStructure(
					Guid.NewGuid(), tenantId, studentId, structure.Id, DefaultAcademicYearId),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	/// <summary>
	/// Creates a ChargeCategory for the seeded academic year and returns its ID.
	/// </summary>
	/// <param name="client">HTTP client to use.</param>
	/// <param name="isApprovalRequired">
	/// When <c>true</c> (default), charges under this category start as Pending.
	/// When <c>false</c>, charges are auto-approved at creation time.
	/// </param>
	public static async Task<Guid> CreateChargeCategoryAsync(
		HttpClient client,
		bool isApprovalRequired = true)
	{
		var request = new CreateChargeCategoryRequest
		{
			Name = TestDataHelper.UniqueName("ChrgCat"),
			IsApprovalRequired = isApprovalRequired,
			Description = "Test charge category",
		};

		var response = await client.PostAsJsonAsync(
			$"{ChargeCategoriesUrl}?academicYearId={DefaultAcademicYearId}", request, JsonOptions);
		await EnsureSuccessAsync(response, "Charge category creation failed");

		var result = await response.Content
			.ReadFromJsonAsync<ChargeCategoryResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null response from charge category creation");

		return result.Id;
	}

	/// <summary>
	/// Creates a charge for the given student and category under the seeded academic year.
	/// Returns the created charge ID.
	/// </summary>
	public static async Task<Guid> CreateChargeAsync(
		HttpClient client,
		Guid studentId,
		Guid categoryId)
	{
		var request = BuildCreateDto(studentId, categoryId);

		var response = await client.PostAsJsonAsync(
			$"{ChargesUrl}?academicYearId={DefaultAcademicYearId}", request, JsonOptions);
		await EnsureSuccessAsync(response, "Charge creation failed");

		var result = await response.Content
			.ReadFromJsonAsync<ChargeResponse>(JsonOptions)
			?? throw new InvalidOperationException("Null response from charge creation");

		return result.Data?.ChargeId
			?? throw new InvalidOperationException("Null ChargeId in charge creation response");
	}

	// ── DTO builders ──────────────────────────────────────────────────────────

	/// <summary>
	/// Builds a <see cref="CreateChargeRequest"/> with all required fields populated.
	/// Uses a future due date and a positive amount.
	/// </summary>
	public static CreateChargeRequest BuildCreateDto(Guid studentId, Guid categoryId)
	{
		return new CreateChargeRequest
		{
			ChargeName = TestDataHelper.UniqueName("Chrg"),
			StudentId = studentId,
			ChargeCategoryId = categoryId,
			PurposeDescription = TestDataHelper.UniqueName("ChrgPurpose"),
			Amount = 500m,
			DueDate = DateTime.UtcNow.AddDays(30),
			Priority = 1,
		};
	}

	// ── Internal helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Throws with the response body content when a request fails, giving a
	/// more actionable failure message than a bare <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
	/// </summary>
	private static async Task EnsureSuccessAsync(HttpResponseMessage response, string context)
	{
		if (response.IsSuccessStatusCode)
			return;

		var body = await response.Content.ReadAsStringAsync();
		var snippet = body.Length > 1000 ? body[..1000] : body;
		throw new InvalidOperationException($"{context} ({(int)response.StatusCode}): {snippet}");
	}
}
