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

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeBatchLifecycleApi_Tests(ShikshaMitraApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	private const string Url = "/api/charges";
	private readonly Guid _checkerId = Guid.NewGuid();

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Every_category_starts_pending_and_rejection_has_its_own_inbox()
	{
		var chargeId = await CreateBatchAsync();
		var detail = await ReadAsync(chargeId);
		detail.WorkflowStatus.ShouldBe(ChargeWorkflowStatus.Pending);
		detail.IsApprovalRequired.ShouldBeTrue();
		detail.Students.ShouldAllBe(line => line.Status == (int)ChargeStatus.Pending);
		using var checker = CreateClientAs(_checkerId);
		var rejection = await checker.PostAsJsonAsync($"{Url}/{chargeId}/reject", new RejectChargeRequest
		{
			ConcurrencyStamp = detail.ConcurrencyStamp, RejectionReason = "Charge request rejected after review."
		}, JsonOptions);
		rejection.StatusCode.ShouldBe(HttpStatusCode.OK, await rejection.Content.ReadAsStringAsync());
		(await rejection.Content.ReadFromJsonAsync<RejectionResponse>(JsonOptions))!
			.Data.WorkflowStatus.ShouldBe(ChargeWorkflowStatus.Rejected);
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.Rejected);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task All_students_must_settle_and_reversed_payments_reopen_the_batch()
	{
		var chargeId = await CreateBatchAsync();
		var detail = await ReadAsync(chargeId);
		using var checker = CreateClientAs(_checkerId);
		var approval = await checker.PostAsJsonAsync($"{Url}/{chargeId}/approve", new ApproveChargeRequest
		{
			ConcurrencyStamp = detail.ConcurrencyStamp, ApprovalNotes = "Approved by the independent checker."
		}, JsonOptions);
		approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.InProcess);
		var repeat = await checker.PostAsJsonAsync($"{Url}/{chargeId}/approve", new ApproveChargeRequest
		{
			ConcurrencyStamp = (await ReadAsync(chargeId)).ConcurrencyStamp
		}, JsonOptions);
		repeat.IsSuccessStatusCode.ShouldBeFalse();
		(await repeat.Content.ReadAsStringAsync()).ShouldContain(FinanceChargeErrorCodes.ChargeNotPending);
		var firstPayment = await SeedPaymentAsync(detail.Students[0], 500d);
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.InProcess);
		await SeedPaymentAsync(detail.Students[1], 200d);
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.InProcess);
		await SeedPaymentAsync(detail.Students[1], 300d);
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.Completed);
		await InTenantAsync(async provider =>
		{
			var payment = await provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(firstPayment);
			payment.MarkReversed("Rejected payment instrument.");
		});
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.InProcess);
		(await ReadAsync(chargeId)).Students[0].OutstandingAmount.ShouldBe(500m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Partial_review_remains_pending_and_waived_balance_can_complete()
	{
		var chargeId = await CreateBatchAsync();
		var detail = await ReadAsync(chargeId);
		using var checker = CreateClientAs(_checkerId);
		var response = await checker.PostAsJsonAsync($"{Url}/{chargeId}/approve", new ApproveChargeRequest
		{
			ConcurrencyStamp = detail.ConcurrencyStamp, StudentIds = [detail.Students[0].StudentId]
		}, JsonOptions);
		response.EnsureSuccessStatusCode();
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.Pending);
		detail = await ReadAsync(chargeId);
		response = await checker.PostAsJsonAsync($"{Url}/{chargeId}/reject", new RejectChargeRequest
		{
			ConcurrencyStamp = detail.ConcurrencyStamp, StudentIds = [detail.Students[1].StudentId],
			RejectionReason = "This student is excluded after review."
		}, JsonOptions);
		response.EnsureSuccessStatusCode();
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.InProcess);
		await SeedPaymentAsync(detail.Students[0], 200d);
		await InTenantAsync(async provider => await provider.GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>()
			.InsertAsync(new ChargeWaiverApplication(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
				detail.Students[0].StudentId, detail.Students[0].ChargeLineId, Factory.SeededAcademicYearId,
				500m, 300m, "Waive the remaining charge balance.", _checkerId, Guid.NewGuid().ToString("N"))));
		await AssertInboxAsync(chargeId, ChargeWorkflowStatus.Completed);
		var settled = (await ReadAsync(chargeId)).Students.Single(line => line.Status == (int)ChargeStatus.Approved);
		settled.CollectedAmount.ShouldBe(200m);
		settled.WaivedAmount.ShouldBe(300m);
		settled.OutstandingAmount.ShouldBe(0m);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Invalid_batch_status_is_rejected()
	{
		var response = await Client.GetAsync($"{Url}?academicYearId={Factory.SeededAcademicYearId}&status=99");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
				TransactionType.Income, $"Charge income {categoryId:N}", null, defaultAccountHeadId: account.Id);
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
