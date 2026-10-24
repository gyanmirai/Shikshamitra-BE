using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargePaymentProgressApi_Tests(FinanceReceiptApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	private const string Url = "/api/charges";
	private readonly Guid _checkerId = Guid.NewGuid();

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Payment_progress_and_cancellation_match_details_after_collection_and_reversal()
	{
		var chargeId = await CreateBatchAsync();
		var detail = await ReadAsync(chargeId);
		if (detail.Students.Any(line => line.Status == (int)ChargeStatus.Pending))
		{
			using var checker = CreateClientAs(_checkerId);
			var approval = await checker.PostAsJsonAsync($"{Url}/{chargeId}/approve", new ApproveChargeRequest
			{
				ConcurrencyStamp = detail.ConcurrencyStamp, ApprovalNotes = "Reviewed charge payment progress."
			}, JsonOptions);
			approval.EnsureSuccessStatusCode();
		}
		await AssertPaymentProgressAsync(chargeId, 0, 2, 0, canCancel: true);
		var firstStudent = detail.Students[0];
		var paymentId = await SeedPaymentAsync(firstStudent, 500d);
		await SeedPaymentAsync(detail.Students[1], 200d);
		await AssertPaymentProgressAsync(chargeId, 1, 1, 0, canCancel: false);
		detail = await ReadAsync(chargeId);
		var cancel = await Client.PostAsJsonAsync($"{Url}/{chargeId}/void", new VoidChargeRequest
		{
			ConcurrencyStamp = detail.ConcurrencyStamp, Reason = "Cancellation must be blocked after collection."
		}, JsonOptions);
		cancel.IsSuccessStatusCode.ShouldBeFalse();
		(await cancel.Content.ReadAsStringAsync()).ShouldContain(FinanceChargeErrorCodes.ChargeAlreadyCollected);
		await InTenantAsync(async provider =>
		{
			var payment = await provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(paymentId);
			payment.MarkReversed("Payment reversed after instrument rejection.");
		});
		await AssertPaymentProgressAsync(chargeId, 0, 2, 0, canCancel: false);
		await InTenantAsync(async provider => await provider.GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>()
			.InsertAsync(new ChargeWaiverApplication(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				firstStudent.StudentId, firstStudent.ChargeLineId, Factory.SeededAcademicYearId,
				500m, 500m, "Full charge waiver after review.", _checkerId, Guid.NewGuid().ToString("N"))));
		await AssertPaymentProgressAsync(chargeId, 0, 1, 1, canCancel: false);
	}

	private async Task AssertPaymentProgressAsync(Guid chargeId, int paid, int outstanding, int waived, bool canCancel)
	{
		var detail = await ReadAsync(chargeId);
		var list = await GetAndReadAsync<ChargeListResponse>(
			$"{Url}?academicYearId={Factory.SeededAcademicYearId}&status={(int)detail.WorkflowStatus}&search={detail.ChargeName}&sortBy=paidStudentCount&sortOrder=desc");
		var item = list.Items.Single(row => row.ChargeId == chargeId);
		item.StudentCount.ShouldBe(detail.Students.Count);
		item.PaidStudentCount.ShouldBe(paid);
		item.OutstandingStudentCount.ShouldBe(outstanding);
		item.WaivedStudentCount.ShouldBe(waived);
		item.AvailableActions.Contains("Void").ShouldBe(canCancel);
		detail.AvailableActions.Contains("Void").ShouldBe(canCancel);
		item.PaidStudentCount.ShouldBe(detail.Students.Count(line => line.Status == (int)ChargeStatus.Approved && line.OutstandingAmount == 0m && line.CollectedAmount > 0m));
		item.OutstandingStudentCount.ShouldBe(detail.Students.Count(line => line.OutstandingAmount > 0m));
		item.WaivedStudentCount.ShouldBe(detail.Students.Count(line => line.WaivedAmount > 0m));
	}

	private async Task<Guid> CreateBatchAsync()
	{
		var firstStudent = Guid.NewGuid();
		var secondStudent = Guid.NewGuid();
		var categoryId = Guid.NewGuid();
		await InTenantAsync(async provider =>
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				Factory.SeededBoardId, Factory.SeededMediumId, null, $"Lifecycle fees {categoryId:N}");
			await provider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
			var structure = new FeeStructure(Guid.NewGuid(), tenantId, metadata.Id, Factory.SeededStandardId,
				$"Lifecycle structure {categoryId:N}");
			await provider.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure);
			foreach (var studentId in new[] { firstStudent, secondStudent })
			{
				await provider.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(
					new Student(studentId, tenantId, $"Lifecycle{studentId:N}", null, "Student",
						Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
						Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId,
						status: StudentLifeCycleStatusEnum.Enrolled));
				await provider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(
					new StudentFeeStructure(Guid.NewGuid(), tenantId, studentId, structure.Id, Factory.SeededAcademicYearId));
			}
			var account = new AccountHead(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				$"Charge revenue {categoryId:N}", AccountHeadType.Income, null, null);
			await provider.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(account);
			var income = new TransactionCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				TransactionType.Income, $"CI {categoryId:N}", null, defaultAccountHeadId: account.Id);
			await provider.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income);
			var category = new ChargeCategory(categoryId, tenantId, Factory.SeededAcademicYearId,
				$"Charge category {categoryId:N}", false, null);
			category.SetAccountingMapping(income.Id, account.Id);
			await provider.GetRequiredService<IRepository<ChargeCategory, Guid>>().InsertAsync(category);
			var checkerName = $"charge-checker-{_checkerId:N}";
			var identityContext = provider.GetRequiredService<ShikshaMitraDbContext>();
			await identityContext.Set<IdentityUser>().AddAsync(
				new IdentityUser(_checkerId, checkerName, $"{checkerName}@example.test", tenantId));
			await identityContext.SaveChangesAsync();
			await provider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: _checkerId, description: "Charge test checker scope"));
		});
		await InTenantAsync(async provider =>
		{
			var scopes = provider.GetRequiredService<IUserScopeService>();
			await scopes.InvalidateUserScopeCacheAsync(_checkerId);
			(await scopes.GetUserScopeAsync(_checkerId)).HasAnyAccess.ShouldBeTrue();
		});
		var response = await Client.PostAsJsonAsync($"{Url}?academicYearId={Factory.SeededAcademicYearId}", new CreateChargeRequest
		{
			ChargeName = $"Lifecycle {categoryId:N}", ChargeCategoryId = categoryId, Amount = 500m,
			PurposeDescription = "Charge lifecycle integration test", DueDate = DateTimeOffset.UtcNow.AddDays(30),
			Audience = new ChargeAudienceDto
			{
				Scope = new AcademicScopeDto { BoardId = Factory.SeededBoardId, EducationMediumId = Factory.SeededMediumId, ClassIds = [Factory.SeededStandardId] },
				Selection = new StudentSelectionDto { Mode = StudentSelectionMode.Explicit, StudentIds = [firstStudent, secondStudent] }
			}
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions))!.Data.ChargeId;
	}

	private async Task<ChargeDetailData> ReadAsync(Guid chargeId) =>
		(await GetAndReadAsync<ChargeDetailResponse>($"{Url}/{chargeId}")).Data;

	private async Task AssertInboxAsync(Guid chargeId, ChargeWorkflowStatus expected)
	{
		(await ReadAsync(chargeId)).WorkflowStatus.ShouldBe(expected);
		foreach (var status in Enum.GetValues<ChargeWorkflowStatus>())
		{
			var list = await GetAndReadAsync<ChargeListResponse>(
				$"{Url}?academicYearId={Factory.SeededAcademicYearId}&status={(int)status}&search={(await ReadAsync(chargeId)).ChargeName}");
			list.Items.Any(item => item.ChargeId == chargeId).ShouldBe(status == expected);
			list.Items.ShouldAllBe(item => item.WorkflowStatus == status && item.DisplayStatus == (int)status);
			list.ProcessedCount.ShouldBe(list.InProcessCount);
			new[] { list.PendingCount, list.InProcessCount, list.CompletedCount, list.RejectedCount }[(int)expected].ShouldBeGreaterThan(0);
		}
	}

	private async Task<Guid> SeedPaymentAsync(ChargeStudentLineDto line, double amount)
	{
		var paymentId = Guid.NewGuid();
		await InTenantAsync(async provider =>
		{
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
			await provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(
				new FeeRecordMetadata(paymentId, tenantId, line.StudentId, Factory.SeededAcademicYearId,
					amount, $"REC-{paymentId:N}", FeeCollectionStatus.Paid));
			await provider.GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(
				new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = line.StudentId,
					ChargeId = line.ChargeLineId, Amount = amount, FeeRecordMetadataId = paymentId, PaymentDate = DateTimeOffset.UtcNow });
		});
		return paymentId;
	}

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var provider = scope.ServiceProvider;
		using (provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			await action(provider);
			await uow.CompleteAsync();
		}
	}
}
