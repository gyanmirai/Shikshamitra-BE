using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<ConsumerRefundIntegrityProof> ReadConsumerRefundIntegrityProofAsync(
		DownstreamRefundFixture fixture, Guid expectedLineChargeId) => WithUnitOfWorkAsync(async () =>
	{
		var owner = fixture.Source.Settlement.Owner;
		var request = await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(fixture.Submitted.Id);
		request.ShouldNotBeNull();
		request.TenantId.ShouldBe(owner.TenantId);
		request.AcademicYearId.ShouldBe(owner.YearId);
		request.EmployeeId.ShouldBe(owner.PersonId);
		request.StudentId.ShouldBeNull();
		request.FinanceFeeAccountId.ShouldBeNull();
		request.Status.ShouldBe(FeeRefundStatus.Processed);
		request.OriginalReceiptId.ShouldBe(fixture.Source.Collection.ReceiptId);
		request.TotalAmount.ShouldBe(15.25m);
		request.CalculatedAmount.ShouldBe(15m);
		request.ProcessedAmount.ShouldBe(15m);
		request.ReversedAmount.ShouldBe(0m);
		var line = request.Lines.ShouldHaveSingleItem();
		line.FeeRefundRequestId.ShouldBe(request.Id);
		line.ChargeId.ShouldBe(expectedLineChargeId);
		line.SourceAllocationId.ShouldBe(fixture.Source.AllocationId);
		line.SourceReceiptId.ShouldBe(fixture.Source.Collection.ReceiptId);
		line.DueFeeId.ShouldBeNull();
		line.LateFeeDueFeeId.ShouldBeNull();
		line.SettledPaidAmount.ShouldBe(40.25m);
		line.RequestedAmount.ShouldBe(15.25m);
		line.CalculatedAmount.ShouldBe(15m);
		line.ProcessedAmount.ShouldBe(15m);
		line.ReversedAmount.ShouldBe(0m);
		var link = await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetAsync(request.FinanceExternalSourceLinkId!.Value);
		link.TenantId.ShouldBe(owner.TenantId);
		link.SourceId.ShouldBe(owner.AssessmentId);
		link.AcademicYearId.ShouldBe(owner.YearId);
		link.FinanceChargeId.ShouldBe(fixture.Source.Settlement.Posted.FinanceChargeId);
		var assessment = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
		assessment.Amount.ShouldBe(100.75m);
		assessment.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
		assessment.FinanceSourceLinkId.ShouldBe(link.Id);
		var allocation = await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().GetAsync(fixture.Source.AllocationId);
		allocation.ChargeId.ShouldBe(link.FinanceChargeId);
		allocation.FeeRecordMetadataId.ShouldBe(fixture.Source.Collection.CollectionId);
		allocation.EmployeeId.ShouldBe(owner.PersonId);
		allocation.StudentId.ShouldBeNull();
		allocation.Amount.ShouldBe(40.25m);
		var header = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(allocation.FeeRecordMetadataId);
		header.TenantId.ShouldBe(owner.TenantId);
		header.AcademicYearId.ShouldBe(owner.YearId);
		header.EmployeeId.ShouldBe(owner.PersonId);
		header.TotalPaidFee.ShouldBe(40.25m);
		var receipt = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(fixture.Source.Collection.ReceiptId);
		receipt.TenantId.ShouldBe(owner.TenantId);
		receipt.AcademicYearId.ShouldBe(owner.YearId);
		receipt.EmployeeId.ShouldBe(owner.PersonId);
		receipt.FeeCollectionTransactionId.ShouldBe(header.Id);
		receipt.TotalPaidAmount.ShouldBe(40.25m);
		return new ConsumerRefundIntegrityProof(request.Id, line.Id, link.Id, link.SourceSnapshotJson, allocation.Id, allocation.ChargeId,
			header.Id, receipt.Id, line.ProcessedAmount, line.ReversedAmount, assessment.Amount, assessment.PaymentStatus);
	});

	private sealed record ConsumerRefundIntegrityProof(Guid RequestId, Guid LineId, Guid LinkId, string AcceptedSourceJson,
		Guid AllocationId, Guid AllocationChargeId, Guid HeaderId, Guid ReceiptId, decimal Processed, decimal Reversed,
		decimal AssessmentPrincipal, ChargePaymentStatus LocalDeclaration);
}
