using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeValidationApi_Tests : ChargeApiTestsBase
{
	public ChargeValidationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Reject_Charge_When_Student_Has_No_Fee_Structure_Assignment()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(
			Client, Factory, assignFeeStructure: false);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		problem.GetProperty("errorCode").GetString()
			.ShouldBe(FinanceChargeErrorCodes.StudentFeeStructureNotAssigned);
	}

	// ── Maker-checker rule ────────────────────────────────────────────────────

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Not_Approve_Own_Charge_Returns_Error_Maker_Checker()
	{
		// Default test user creates the charge (TestAuthHandler.TestUserId is the RequestorId)
		var (chargeId, _, _) = await CreatePendingChargeAsync();

		// Same default test user tries to approve — must be rejected by domain guard
		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/approve",
			new ApproveChargeRequest { ApprovalNotes = "Self-approve attempt" },
			JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Not_Reject_Own_Charge_Returns_Error_Maker_Checker()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/reject",
			new RejectChargeRequest { RejectionReason = "Self-reject attempt" },
			JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	// ── Reference-data validation ─────────────────────────────────────────────

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Nonexistent_StudentId()
	{
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(Guid.NewGuid(), categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Nonexistent_AcademicYearId()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={Guid.NewGuid()}", request, JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Create_With_Nonexistent_ChargeCategoryId()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var request = ChargeTestHelper.BuildCreateDto(studentId, Guid.NewGuid());

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.IsSuccessStatusCode.ShouldBeFalse();
	}

	// ── Amount validation ─────────────────────────────────────────────────────

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_400_Create_With_Zero_Amount()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
		request.Amount = 0m;

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_400_Create_With_Negative_Amount()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
		request.Amount = -1m;

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// ── Description validation ────────────────────────────────────────────────

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_400_Create_With_Empty_PurposeDescription()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client);
		var request = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
		request.PurposeDescription = string.Empty;

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ChargeTestHelper.DefaultAcademicYearId}", request, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// ── Approval/rejection state guards ──────────────────────────────────────

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Approve_Already_Approved_Charge()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		using var approverClient = CreateClientAs(Guid.NewGuid());

		// First approval succeeds
		(await approverClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/approve",
			new ApproveChargeRequest { ApprovalNotes = "First approve" },
			JsonOptions)).EnsureSuccessStatusCode();

		// Second approval on now-approved charge must fail
		var secondAttempt = await approverClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/approve",
			new ApproveChargeRequest { ApprovalNotes = "Second approve" },
			JsonOptions);

		secondAttempt.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_Error_Reject_Already_Rejected_Charge()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		using var rejectorClient = CreateClientAs(Guid.NewGuid());

		// First rejection succeeds
		(await rejectorClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/reject",
			new RejectChargeRequest { RejectionReason = "First rejection" },
			JsonOptions)).EnsureSuccessStatusCode();

		// Second rejection on now-rejected charge must fail
		var secondAttempt = await rejectorClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/reject",
			new RejectChargeRequest { RejectionReason = "Second rejection" },
			JsonOptions);

		secondAttempt.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Return_400_Reject_With_Empty_RejectionReason()
	{
		var (chargeId, _, _) = await CreatePendingChargeAsync();
		using var rejectorClient = CreateClientAs(Guid.NewGuid());

		var response = await rejectorClient.PostAsJsonAsync(
			$"{BaseUrl}/{chargeId}/reject",
			new RejectChargeRequest { RejectionReason = string.Empty },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	#endregion
}
