using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

[Collection(FinanceReceiptApiTestCollection.Name)]
public partial class FinanceExternalReceivableApi_Tests(FinanceReceiptApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string BaseUrl = "/api/finance/external-receivables";

	[Theory]
	[InlineData("POST", "")]
	[InlineData("GET", "")]
	[InlineData("GET", "/collect-form")]
	[InlineData("POST", "/settlement-policy")]
	[InlineData("POST", "/approve")]
	[InlineData("POST", "/collections")]
	[InlineData("POST", "/waivers")]
	[InlineData("POST", "/waivers/approve")]
	[InlineData("POST", "/waivers/reject")]
	[InlineData("POST", "/waivers/reverse")]
	public async Task External_Receivable_Routes_Should_Require_Their_Action_Permission(string method, string suffix)
	{
		using var denied = ClientWithPermissions("__none__");
		var source = new FinanceExternalSourceReference
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = Guid.NewGuid(), SourceVersion = "permission-boundary-v1"
		};
		using var response = method == "GET"
			? await denied.GetAsync(BaseUrl + suffix + SourceQuery(source))
			: await denied.PostAsJsonAsync(BaseUrl + suffix, PermissionBoundaryInput(suffix, source), JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Reviewed_Student_Assessment_Without_A_Fee_Assignment_Should_Post_Read_And_Replay_Over_Http()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var fixture = await SeedReviewedAssessmentAsync();
		using var cashier = ClientWithPermissions(string.Join(',',
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View));
		var input = new PostFinanceExternalReceivableInput
		{
			Source = fixture.Source, FinanceChargeCategoryId = fixture.FinanceCategoryId,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		};

		using var postedResponse = await cashier.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		postedResponse.IsSuccessStatusCode.ShouldBeTrue(await postedResponse.Content.ReadAsStringAsync());
		var posted = await postedResponse.Content.ReadFromJsonAsync<FinanceExternalReceivableDto>(JsonOptions);
		posted.ShouldNotBeNull();
		using var readResponse = await cashier.GetAsync(BaseUrl + SourceQuery(fixture.Source));
		readResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await readResponse.Content.ReadAsStringAsync());
		var read = await readResponse.Content.ReadFromJsonAsync<FinanceExternalReceivableDto>(JsonOptions);
		read.ShouldNotBeNull();
		using var replayResponse = await cashier.PostAsJsonAsync(BaseUrl, input, JsonOptions);
		replayResponse.IsSuccessStatusCode.ShouldBeTrue(await replayResponse.Content.ReadAsStringAsync());
		var replay = await replayResponse.Content.ReadFromJsonAsync<FinanceExternalReceivableDto>(JsonOptions);
		replay.ShouldNotBeNull();

		posted.FinanceChargeId.ShouldNotBe(Guid.Empty);
		posted.FinanceChargeRequestId.ShouldNotBe(Guid.Empty);
		foreach (var result in new[] { posted, read, replay })
		{
			result.Source.Kind.ShouldBe(FinanceExternalSourceKind.LibraryAssessment);
			result.Source.SourceId.ShouldBe(fixture.Source.SourceId);
			result.Source.SourceVersion.ShouldBe(fixture.Source.SourceVersion);
			result.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
			result.Payer.Kind.ShouldBe(FinanceExternalPayerKind.Student);
			result.Payer.StudentId.ShouldBe(fixture.StudentId);
			result.Payer.EmployeeId.ShouldBeNull();
			result.OriginalAmount.ShouldBe(100.75m);
			result.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
			result.OutstandingAmount.ShouldBe(0m, "An assessment posted for independent approval is not yet collectible.");
			result.AcceptedPaymentAmount.ShouldBe(0m);
			result.SettledPaymentAmount.ShouldBe(0m);
			result.Currency.ShouldBe("INR");
			result.AssessmentDate.ShouldBe(new DateOnly(2026, 11, 17));
			result.DueDate.ShouldBe(new DateOnly(2026, 11, 24));
			result.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
			result.FinanceChargeRequestId.ShouldBe(posted.FinanceChargeRequestId);
		}
		await InSchoolAsync(async services =>
		{
			var assessment = await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(fixture.Source.SourceId);
			var links = await services.GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.SourceId == fixture.Source.SourceId);
			links.Count.ShouldBe(1);
			assessment.FinanceSourceLinkId.ShouldBe(links[0].Id);
			assessment.Amount.ShouldBe(100.75m);
			assessment.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
			(await services.GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>()
				.CountAsync(row => row.SourceLinkId == links[0].Id && row.Operation == "Post" && row.IdempotencyKey == input.IdempotencyKey)).ShouldBe(1);
			(await services.GetRequiredService<IRepository<Charge, Guid>>()
				.CountAsync(row => row.ChargeRequestId == posted.FinanceChargeRequestId)).ShouldBe(1);
			(await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.CountAsync(row => row.StudentId == fixture.StudentId)).ShouldBe(0);
		});
	}

	private HttpClient ClientWithPermissions(string permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Cashier");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions);
		return client;
	}

	private static string SourceQuery(FinanceExternalSourceReference source) =>
		$"?kind={(int)source.Kind}&sourceId={source.SourceId:D}&sourceVersion={Uri.EscapeDataString(source.SourceVersion)}";

	private static object PermissionBoundaryInput(string suffix, FinanceExternalSourceReference source)
	{
		var key = Guid.NewGuid().ToString("N");
		return suffix switch
		{
			"" => new PostFinanceExternalReceivableInput
			{
				Source = source, FinanceChargeCategoryId = Guid.NewGuid(), IdempotencyKey = key
			},
			"/settlement-policy" => new BindFinanceExternalSettlementPolicyInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key,
				CollectionPolicyVersionId = Guid.NewGuid(), Reason = "Confirm the reviewed settlement policy."
			},
			"/approve" => new FinanceExternalApprovalInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key, Reason = "Reviewed the assessment."
			},
			"/collections" => new FinanceExternalCollectInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key,
				SchoolReceiptDate = new(2026, 11, 17), Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 10m }]
			},
			"/waivers" => new FinanceExternalWaiverInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key, Amount = 10m, Reason = "Reviewed waiver request."
			},
			"/waivers/approve" or "/waivers/reject" => new FinanceExternalWaiverDecisionInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key,
				WaiverRequestId = Guid.NewGuid(), Reason = "Reviewed waiver evidence."
			},
			"/waivers/reverse" => new ReverseFinanceExternalWaiverInput
			{
				Source = source, Revision = "permission-boundary-revision", IdempotencyKey = key,
				WaiverApplicationId = Guid.NewGuid(), Amount = 10m, Reason = "Correct the recorded waiver."
			},
			_ => throw new ArgumentOutOfRangeException(nameof(suffix))
		};
	}

	private async Task<ReviewedAssessment> SeedReviewedAssessmentAsync()
	{
		ReviewedAssessment? result = null;
		await InSchoolAsync(async services =>
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			var actorId = Guid.Parse(TestAuthHandler.TestUserId);
			var actor = await services.GetRequiredService<IIdentityUserRepository>().GetAsync(actorId);
			actor.TenantId.ShouldBe(tenantId);
			actor.IsActive.ShouldBeTrue();
			var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, "HTTP borrower",
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
				Factory.SeededStandardId, Factory.SeededDivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
				GenderEnum.Female, primaryContactNo: "9999999999");
			student.AdditionalDetail!.IsLibraryMember = true;
			await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
			(await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.CountAsync(row => row.StudentId == student.Id)).ShouldBe(0);
			var libraryCategory = new LibraryChargeCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				$"HTTP assessment {student.Id:N}", 100.75m, false, "Reviewed damage evidence");
			await services.GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(libraryCategory, true);
			var assessment = new LibraryCharge(Guid.NewGuid(), student.Id, 0, libraryCategory.Id, null, 100.75m,
				new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc), tenantId)
				{ Remarks = "Reviewed damage assessment from the original Library register." };
			assessment.ConfirmSourceContext(Factory.SeededAcademicYearId, LibraryAcademicYearProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { AssessmentId = assessment.Id, AcademicYearId = Factory.SeededAcademicYearId,
					ReviewedBy = actorId, Reason = "Checked the assessment's original transaction year." }),
				new(2026, 11, 17), new(2026, 11, 24), LibraryCivilDateProvenance.ReviewedLegacy, LibraryCivilDateProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { ReviewedBy = actorId, Reason = "Checked the original school assessment dates." }), "INR");
			await services.GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(assessment, true);
			var income = new TransactionCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				TransactionType.Income, $"Library {student.Id:N}", null);
			await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
			var financeCategory = new ChargeCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				$"HTTP Library charge {student.Id:N}", true, null);
			financeCategory.SetAccountingMapping(income.Id);
			await services.GetRequiredService<IRepository<ChargeCategory, Guid>>().InsertAsync(financeCategory, true);
			result = new(student.Id, financeCategory.Id, new()
			{
				Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = assessment.Id,
				SourceVersion = assessment.GetAssessmentVersion()
			});
		});
		return result ?? throw new InvalidOperationException("The reviewed assessment was not seeded.");
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}

	private sealed record ReviewedAssessment(Guid StudentId, Guid FinanceCategoryId, FinanceExternalSourceReference Source);
}
