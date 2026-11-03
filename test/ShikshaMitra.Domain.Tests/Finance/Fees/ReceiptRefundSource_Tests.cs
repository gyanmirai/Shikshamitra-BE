using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ReceiptRefundSource_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Late_fee_refund_uses_charge_document_for_new_collections_and_preserves_legacy_fee_document(
		bool legacy, bool verifiedProvisional)
	{
		var fixture = new Fixture(legacy, verifiedProvisional);
		var service = new PaidLateFeeRefundService(fixture.Dues, fixture.Heads, fixture.LateAllocations,
			fixture.Collections, fixture.Receipts, fixture.Refunds);

		var sources = await service.GetSourcesAsync(fixture.StudentId, fixture.YearId, assignmentId: fixture.AssignmentId);
		var source = Assert.Single(sources);
		var expected = legacy ? fixture.FeeReceipt : fixture.ChargeReceipt;
		Assert.Equal(expected.Id, source.ReceiptId);
		Assert.Equal(expected.ReceiptNumber, source.ReceiptNumber);
		Assert.Equal(fixture.LateAllocation.Id, source.AllocationId);
		Assert.Equal(60_000m, source.Paid);
		Assert.Equal(59_000m, source.Available);

		var sibling = legacy ? fixture.ChargeReceipt : fixture.FeeReceipt;
		Assert.Empty(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId,
			receiptId: sibling.Id, assignmentId: fixture.AssignmentId));
		Assert.Single(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId,
			receiptId: expected.Id, assignmentId: fixture.AssignmentId));
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Ordinary_charge_refund_uses_same_owned_document_without_rewriting_legacy_evidence(
		bool legacy, bool verifiedProvisional)
	{
		var fixture = new Fixture(legacy, verifiedProvisional);
		var service = new PaidChargeRefundService(fixture.Charges, fixture.ChargeAllocations,
			fixture.Collections, fixture.Receipts, fixture.Refunds);

		var source = Assert.Single(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId));
		var expected = legacy ? fixture.FeeReceipt : fixture.ChargeReceipt;
		Assert.Equal(expected.Id, source.ReceiptId);
		Assert.Equal(fixture.ChargeAllocation.Id, source.AllocationId);
		Assert.Equal(600m, source.Paid);
		Assert.Equal(575m, source.Available);
		var sibling = legacy ? fixture.ChargeReceipt : fixture.FeeReceipt;
		Assert.Empty(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId, receiptId: sibling.Id));
		Assert.Single(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId, receiptId: expected.Id));
	}

	[Theory]
	[InlineData(FeeReceiptVerificationStatus.PendingVerification)]
	[InlineData(FeeReceiptVerificationStatus.Rejected)]
	public async Task Unverified_charge_receipt_cannot_supply_refundable_late_fees(FeeReceiptVerificationStatus status)
	{
		var fixture = new Fixture(false, true);
		fixture.ChargeReceipt.VerificationStatus = status;
		var service = new PaidLateFeeRefundService(fixture.Dues, fixture.Heads, fixture.LateAllocations,
			fixture.Collections, fixture.Receipts, fixture.Refunds);
		Assert.Empty(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId, assignmentId: fixture.AssignmentId));
	}

	[Theory]
	[InlineData(FeeCollectionStatus.InReconciliation)]
	[InlineData(FeeCollectionStatus.Reversed)]
	public async Task Unsettled_or_reversed_collection_cannot_supply_refundable_late_fees(FeeCollectionStatus status)
	{
		var fixture = new Fixture(false, false, status);
		var service = new PaidLateFeeRefundService(fixture.Dues, fixture.Heads, fixture.LateAllocations,
			fixture.Collections, fixture.Receipts, fixture.Refunds);
		Assert.Empty(await service.GetSourcesAsync(fixture.StudentId, fixture.YearId, assignmentId: fixture.AssignmentId));
	}

	private sealed class Fixture
	{
		public Guid StudentId { get; } = Guid.NewGuid();
		public Guid YearId { get; } = Guid.NewGuid();
		public Guid AssignmentId { get; } = Guid.NewGuid();
		public IDueFeeRepository Dues { get; } = Substitute.For<IDueFeeRepository>();
		public IRepository<FeeHead, Guid> Heads { get; } = Substitute.For<IRepository<FeeHead, Guid>>();
		public IRepository<LateFeeCollectionAllocation, Guid> LateAllocations { get; } = Substitute.For<IRepository<LateFeeCollectionAllocation, Guid>>();
		public IRepository<Charge, Guid> Charges { get; } = Substitute.For<IRepository<Charge, Guid>>();
		public IRepository<ChargeCollectionRecord, Guid> ChargeAllocations { get; } = Substitute.For<IRepository<ChargeCollectionRecord, Guid>>();
		public IRepository<FeeRecordMetadata, Guid> Collections { get; } = Substitute.For<IRepository<FeeRecordMetadata, Guid>>();
		public IRepository<FeePaymentReceipt, Guid> Receipts { get; } = Substitute.For<IRepository<FeePaymentReceipt, Guid>>();
		public IFeeRefundRepository Refunds { get; } = Substitute.For<IFeeRefundRepository>();
		public FeePaymentReceipt FeeReceipt { get; }
		public FeePaymentReceipt ChargeReceipt { get; }
		public LateFeeCollectionAllocation LateAllocation { get; }
		public ChargeCollectionRecord ChargeAllocation { get; }

		public Fixture(bool legacy, bool verifiedProvisional, FeeCollectionStatus status = FeeCollectionStatus.Paid)
		{
			var tenant = Guid.NewGuid();
			var head = new FeeHead(Guid.NewGuid(), tenant, YearId, Guid.NewGuid(), "Tuition", null,
				false, null, Guid.NewGuid());
			var due = new DueFee(Guid.NewGuid(), tenant, StudentId, head.Id, (MonthEnum)0, 2026,
				false, 20_000m, new DateTime(2026, 1, 1));
			due.BindAssignment(AssignmentId);
			var collection = new FeeRecordMetadata(Guid.NewGuid(), tenant, StudentId, YearId,
				80_600d, "COUNTER", status);
			FeeReceipt = Receipt(FeeReceiptScope.Fee, legacy ? 80_000d : 20_000d);
			ChargeReceipt = Receipt(FeeReceiptScope.Charge, legacy ? 600d : 60_600d);
			if (!legacy)
			{
				FeeReceipt.SetLateFeePaidAmount(0m);
				ChargeReceipt.SetLateFeePaidAmount(60_000m);
			}
			LateAllocation = new LateFeeCollectionAllocation(Guid.NewGuid(), tenant, collection.Id, due.Id, 60_000d);
			var charge = new Charge(Guid.NewGuid(), tenant, YearId, Guid.NewGuid(), "Library charge",
				Guid.NewGuid(), StudentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
				"Replacement library book", 600m, DateTimeOffset.UtcNow.AddDays(1), (ChargePriority)0,
				ChargeStatus.Approved, Guid.NewGuid());
			ChargeAllocation = new ChargeCollectionRecord(Guid.NewGuid())
			{
				TenantId = tenant, StudentId = StudentId, ChargeId = charge.Id, Amount = 600d,
				FeeRecordMetadataId = collection.Id,
			};
			ReadRows(Dues, [due]);
			ReadRows(Heads, [head]);
			ReadRows(Collections, [collection]);
			ReadRows(Receipts, [FeeReceipt, ChargeReceipt]);
			ReadRows(LateAllocations, [LateAllocation]);
			ReadRows(Charges, [charge]);
			ReadRows(ChargeAllocations, [ChargeAllocation]);
			Refunds.GetCommittedAllocationAmountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(),
				Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(call =>
				Task.FromResult(new Dictionary<Guid, decimal>
				{
					[LateAllocation.Id] = 1_000m, [ChargeAllocation.Id] = 25m,
				}));

			FeePaymentReceipt Receipt(FeeReceiptScope scope, double amount) => new(Guid.NewGuid())
			{
				TenantId = tenant, StudentId = StudentId, AcademicYearId = YearId,
				FeeCollectionTransactionId = collection.Id, ReceiptScope = scope, TotalPaidAmount = amount,
				ReceiptNumber = $"{scope}-TEST", ReceiptDate = new DateOnly(2026, 10, 30),
				ReceiptKind = (scope, verifiedProvisional) switch
				{
					(FeeReceiptScope.Fee, false) => FeeReceiptKind.FinalFee,
					(FeeReceiptScope.Fee, true) => FeeReceiptKind.ProvisionalFee,
					(FeeReceiptScope.Charge, false) => FeeReceiptKind.FinalCharge,
					_ => FeeReceiptKind.ProvisionalCharge,
				},
				VerificationStatus = verifiedProvisional ? FeeReceiptVerificationStatus.Verified : FeeReceiptVerificationStatus.NotApplicable,
			};
		}
	}

	// Execute each real production predicate against immutable source rows. No database or ABP host is needed.
	private static void ReadRows<T>(IRepository<T, Guid> repository, List<T> rows) where T : class, IEntity<Guid>
	{
		repository.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => Task.FromResult(rows.Where(call.ArgAt<Expression<Func<T, bool>>>(0).Compile()).ToList()));
	}
}
