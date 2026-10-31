using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class ReceiptReconciliationHeaderScopeApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[Fact]
	public async Task Display_filters_preserve_student_applicant_tenant_and_restricted_class_scope()
	{
		var current = await SeedAsync(TenantId);
		var foreign = await SeedAsync(Guid.NewGuid());
		using var restricted = CreateClientAs(current.UserId);
		restricted.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		foreach (var receipts in new[] { true, false })
		{
			var column = receipts ? "totalPaidAmount" : "amount";
			var url = Url(current, receipts);
			await AssertAsync(Client, url, column, "greaterThan", 900, 4);
			await AssertAsync(restricted, url, column, "greaterThan", 900, 2);
			await AssertAsync(restricted, url, "studentName", "equals", "Header Student (456789)", 1);
			await AssertAsync(restricted, url, "studentName", "equals", "Header Applicant", 1);
			await AssertAsync(restricted, url + "&search=Header%20Applicant", "studentName", "contains", "Student", 0);
			await AssertAsync(restricted, url + "&search=456789", "studentName", "contains", "Student", 1);
			await AssertAsync(Client, Url(foreign, receipts), column, "greaterThan", 900, 0);
			if (receipts)
			{
				await AssertAsync(restricted, url, "paymentMethodLabel", "equals", "Not recorded", 1);
				await AssertAsync(restricted, url, "verificationStatus", "equals", "Receipt: LINKED-FINAL", 1);
				await AssertAsync(restricted, url + $"&standardId={current.HiddenClassId}", column, "greaterThan", 900, 0);
			}
			else
			{
				await AssertAsync(restricted, url, "standardName", "equals", "Visible", 2);
				await AssertAsync(restricted, url, "bankName", "equals", "Hidden payer bank", 0);
			}
		}
	}

	private static string Url(Context context, bool receipts) => receipts
		? $"/api/v1/fee-receipts?academicYearId={context.YearId}&boardId={context.BoardId}&educationMediumId={context.MediumId}&receiptKind=ProvisionalFee&maxResultCount=20"
		: $"/api/v1/finance/reconciliations?academicYearId={context.YearId}&pageSize=20";

	private static async Task AssertAsync(HttpClient client, string url, string column, string type, object value, int count)
	{
		var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { new { column, type, value } }));
		var response = await client.GetAsync(url + "&filters=" + filters);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		result.GetProperty("totalCount").GetInt32().ShouldBe(count, column + "=" + value);
		result.GetProperty("items").GetArrayLength().ShouldBe(count);
	}

	private async Task<Context> SeedAsync(Guid tenantId)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		async Task<T> Save<T>(T entity) where T : class, IEntity<Guid> =>
			await services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
		var year = await Save(await services.GetRequiredService<AcademicYearManager>().CreateAsync("Header " + Guid.NewGuid().ToString("N"), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)));
		var board = await Save(await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE, year.StartDate, year.EndDate));
		var medium = await Save(await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
		var stage = await Save(await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
		var visible = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Visible", 6, null));
		var hidden = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Hidden", 6, null));
		var userId = Guid.NewGuid();
		await Save(new IdentityUser(userId, "header-" + userId.ToString("N"), userId.ToString("N") + "@example.test", tenantId));
		await Save(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, academicYearId: year.Id,
			educationBoardId: board.Id, educationMediumId: medium.Id, educationMediumStageId: stage.Id, standardId: visible.Id));
		foreach (var standard in new[] { visible, hidden })
		foreach (var applicant in new[] { false, true })
		{
			Guid? studentId = null;
			Guid? accountId = null;
			if (applicant)
			{
				var account = await Save(new FinanceFeeAccount(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), year.Id,
					board.Id, medium.Id, stage.Id, standard.Id, "Header Applicant"));
				accountId = account.Id;
			}
			else
			{
				var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Header", null, "Student", year.Id,
					board.Id, medium.Id, stage.Id, standard.Id, null, DateTimeOffset.UtcNow.AddYears(-12), GenderEnum.Male, "9876543210");
				student.AssignGrNo(standard.Id == visible.Id ? 456789 : 456790);
				studentId = (await Save(student)).Id;
			}
			var tx = await Save(new FeeRecordMetadata(Guid.NewGuid(), tenantId, studentId, year.Id, 999d,
				"HEADER-" + Guid.NewGuid().ToString("N")[..16], FeeCollectionStatus.InReconciliation, financeFeeAccountId: accountId));
			// Applicant legacy row deliberately has no payment record to exercise Not recorded.
			if (!applicant)
				await Save(PaymentMethodRecord.Create(studentId, tx.Id, 999m, DateTimeOffset.UtcNow, PaymentMethodEnum.CreditCard,
					new CreditCardDetails("000042", "0042", "Hidden payer bank"), tenantId));
			Guid? linkedId = null;
			if (applicant)
			{
				var final = new FeePaymentReceipt(Guid.NewGuid()) { TenantId = tenantId, StudentId = studentId,
					FeeCollectionTransactionId = tx.Id, AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = medium.Id,
					ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Fee, ReceiptNumber = standard.Id == visible.Id ? "LINKED-FINAL" : "LINKED-HIDDEN",
					ReceiptDate = new DateOnly(2026, 6, 12), TotalPaidAmount = 1d };
				final.SetFinanceFeeAccount(accountId!.Value);
				await Save(final);
				linkedId = final.Id;
			}
			var receipt = new FeePaymentReceipt(Guid.NewGuid()) { TenantId = tenantId, StudentId = studentId,
				FeeCollectionTransactionId = tx.Id, AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = medium.Id,
				ReceiptKind = FeeReceiptKind.ProvisionalFee, ReceiptScope = FeeReceiptScope.Fee, VerificationStatus = FeeReceiptVerificationStatus.PendingVerification,
				ReceiptNumber = "HEADER-" + Guid.NewGuid().ToString("N")[..16], ReceiptDate = new DateOnly(2026, 6, 12), TotalPaidAmount = 999d, LinkedFinalReceiptId = linkedId };
			if (accountId.HasValue) receipt.SetFinanceFeeAccount(accountId.Value);
			await Save(receipt);
		}
		await uow.CompleteAsync();
		return new Context(year.Id, board.Id, medium.Id, hidden.Id, userId);
	}

	private sealed record Context(Guid YearId, Guid BoardId, Guid MediumId, Guid HiddenClassId, Guid UserId);
}
