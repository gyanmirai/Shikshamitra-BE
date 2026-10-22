using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students;
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

	private const string ChargeCategoriesUrl = "/api/charge-categories";
	private const string ChargesUrl = "/api/charges";

	// ── Static defaults set by the test class constructor from the factory ────

	public static Guid DefaultAcademicYearId { get; set; }
	public static Guid DefaultBoardId { get; set; }
	public static Guid DefaultMediumId { get; set; }
	public static Guid DefaultStandardId { get; set; }
	public static Guid DefaultDivisionId { get; set; }

	// ── Prerequisite data factory methods ────────────────────────────────────

	/// <summary>
	/// Seeds an enrolled student with all four enrollment scope fields
	/// populated so the student satisfies the <c>ChargeManager</c> scope guard.
	/// Returns the student ID and derived board ID.
	/// </summary>
	public static async Task<(Guid StudentId, Guid BoardId)> CreateStudentAsync(
		HttpClient client,
		ShikshaMitraApiTestFactory factory,
		bool assignFeeStructure = true)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var studentId = Guid.NewGuid();
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var student = new Student(studentId, tenantId, TestDataHelper.UniqueName("Charge"), null, "Student",
				DefaultAcademicYearId, factory.SeededBoardId, DefaultMediumId, factory.SeededMediumStageId,
				DefaultStandardId, DefaultDivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
			await scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: false);
			await uow.CompleteAsync();
		}
		if (assignFeeStructure) await AddFeeStructureAssignmentAsync(studentId, factory);
		return (studentId, factory.SeededBoardId);
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
			var structure = structures.FirstOrDefault();
			if (structure == null)
			{
				// Own the prerequisite graph instead of depending on optional demo seeders.
				var income = new TransactionCategory(Guid.NewGuid(), tenantId, DefaultAcademicYearId,
					TransactionType.Income, "Charge fixture income", null);
				var category = new FeeCategory(Guid.NewGuid(), tenantId, DefaultAcademicYearId, "Charge fixture fees", null);
				var head = new FeeHead(Guid.NewGuid(), tenantId, DefaultAcademicYearId, category.Id,
					"Charge fixture tuition", null, false, null, income.Id);
				var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, DefaultAcademicYearId,
					factory.SeededBoardId, DefaultMediumId, null, "Charge fixture structure");
				structure = new FeeStructure(Guid.NewGuid(), tenantId, metadata.Id, DefaultStandardId, "Charge fixture class fees");
				structure.AddDetail(new FeeStructureDetail(Guid.NewGuid(), tenantId, structure.Id,
					head.Id, 1000m, false, false, null, null));
				structure.Lock();
				await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income);
				await provider.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category);
				await provider.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head);
				await provider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
				await provider.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure);
			}
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
		var incomeResponse = await client.PostAsJsonAsync($"/api/income-categories?academicYearId={DefaultAcademicYearId}",
			new { name = $"Charge income {Guid.NewGuid():N}"[..39], tags = Array.Empty<string>() }, JsonOptions);
		await EnsureSuccessAsync(incomeResponse, "Charge category income mapping creation failed");
		var income = await incomeResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		var request = new CreateChargeCategoryRequest
		{
			Name = TestDataHelper.UniqueName("ChrgCat"),
			IsApprovalRequired = isApprovalRequired,
			IncomeCategoryId = income.GetProperty("id").GetGuid(),
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
			Audience = new ChargeAudienceDto {
				Scope = new AcademicScopeDto { BoardId = DefaultBoardId, EducationMediumId = DefaultMediumId,
					ClassIds = [DefaultStandardId], DivisionIds = [DefaultDivisionId] },
				Selection = new StudentSelectionDto { Mode = StudentSelectionMode.Explicit, StudentIds = [studentId] }
			},
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
