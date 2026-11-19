using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class CategoryReferenceValidation_Tests
{
	[Fact]
	public async Task Charge_request_routing_waits_for_the_lock_before_reading_a_remappable_charge_category()
	{
		var categories = Substitute.For<IRepository<ChargeCategory, Guid>>();
		var referenceLock = Substitute.For<ITransactionCategoryReferenceLock>();
		var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		referenceLock.AcquireAsync().Returns(released.Task);
		var categoryId = Guid.NewGuid();
		var request = new ChargeRequest(Guid.NewGuid(), null, Guid.NewGuid(), "Charge", categoryId,
			"Purpose", 10m, DateTimeOffset.UtcNow.AddDays(1), (ChargePriority)0, Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "{}");
		var manager = new ChargeRequestManager(Substitute.For<IRepository<ChargeRequest, Guid>>(),
			Substitute.For<IRepository<Charge, Guid>>(), Substitute.For<IStudentRepository>(), categories,
			null!, referenceLock, null!, Substitute.For<IGuidGenerator>(), NullLogger<ChargeRequestManager>.Instance);

		var settingRouting = manager.SetIncomeCategoryFromChargeCategoryAsync(request);
		await categories.DidNotReceive().FindAsync(categoryId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
		released.SetResult();

		(await Should.ThrowAsync<BusinessException>(() => settingRouting)).Code.ShouldBe(FinanceChargeErrorCodes.ChargeCategoryNotFound);
		await categories.Received(1).FindAsync(categoryId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_new_reference_waits_for_the_transaction_lock_before_reading_the_category()
	{
		var categories = Substitute.For<IRepository<TransactionCategory, Guid>>();
		var referenceLock = Substitute.For<ITransactionCategoryReferenceLock>();
		var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		referenceLock.AcquireAsync().Returns(released.Task);
		var category = new TransactionCategory(Guid.NewGuid(), null, Guid.NewGuid(), TransactionType.Income, "Income", null);
		categories.FindAsync(category.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(category);
		var manager = new FeeHeadAccountingManager(categories, referenceLock);

		var validating = manager.GetValidMappingAsync(category.AcademicYearId, category.Id, creatingReference: true);
		await categories.DidNotReceive().FindAsync(category.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>());
		released.SetResult();

		(await validating).IncomeCategory.ShouldBe(category);
		await referenceLock.Received(1).AcquireAsync();
		await categories.Received(1).FindAsync(category.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_category_deleted_while_a_writer_waits_cannot_be_referenced()
	{
		var categories = Substitute.For<IRepository<TransactionCategory, Guid>>();
		var referenceLock = Substitute.For<ITransactionCategoryReferenceLock>();
		var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		referenceLock.AcquireAsync().Returns(released.Task);
		var manager = new FeeHeadAccountingManager(categories, referenceLock);
		var validating = manager.GetValidMappingAsync(Guid.NewGuid(), Guid.NewGuid(), creatingReference: true);
		released.SetResult();

		(await Should.ThrowAsync<BusinessException>(() => validating)).Code
			.ShouldBe(ShikshaMitra.Finance.Fees.Settings.FinanceFeeHeadErrorCodes.InvalidIncomeCategory);
	}

	[Fact]
	public async Task Read_validation_does_not_acquire_a_write_transaction_lock()
	{
		var categories = Substitute.For<IRepository<TransactionCategory, Guid>>();
		var referenceLock = Substitute.For<ITransactionCategoryReferenceLock>();
		var category = new TransactionCategory(Guid.NewGuid(), null, Guid.NewGuid(), TransactionType.Income, "Income", null);
		categories.FindAsync(category.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(category);
		var manager = new FeeHeadAccountingManager(categories, referenceLock);

		(await manager.GetValidMappingAsync(category.AcademicYearId, category.Id)).IncomeCategory.ShouldBe(category);
		await referenceLock.DidNotReceive().AcquireAsync();
	}
}
