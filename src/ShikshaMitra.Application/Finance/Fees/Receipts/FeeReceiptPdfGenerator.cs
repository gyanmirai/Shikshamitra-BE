using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

/// <summary>All Finance collection entry points and reprints use the same saved evidence.</summary>
public class FeeReceiptPdfGenerator(
	IReadOnlyRepository<FinanceFeeAccount, Guid>? accounts = null,
	IReadOnlyRepository<PaymentMethodRecord, Guid>? paymentRecords = null,
	IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid>? allocations = null,
	IFeePaymentReceiptRepository? receipts = null,
	ICurrentTenant? currentTenant = null,
	IReceiptSchoolIdentityReader? schoolIdentity = null,
	IReadOnlyRepository<FeeRecordMetadata, Guid>? transactions = null,
	IReadOnlyRepository<Standard, Guid>? standards = null,
	IReadOnlyRepository<Division, Guid>? divisions = null,
	IReadOnlyRepository<StudentFamilyDetail, Guid>? families = null,
	IAsyncQueryableExecuter? queryExecuter = null) : IFeeReceiptPdfGenerator, ITransientDependency
{
	public async Task<byte[]> GenerateAsync(FeePaymentReceipt receipt, Student? student,
		AcademicYear? academicYear, CancellationToken cancellationToken = default)
	{
		var account = receipt.FinanceFeeAccountId.HasValue && accounts != null
			? await accounts.GetAsync(receipt.FinanceFeeAccountId.Value, cancellationToken: cancellationToken) : null;
		var metadata = transactions == null ? null
			: await transactions.FindAsync(receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		// A student's current placement may differ from the original payment context.
		var organization = ReceiptCollectionSnapshot.ReadOrganization(metadata) ?? account?.MediumStageId;
		var school = schoolIdentity == null ? new ReceiptSchoolIdentity(currentTenant?.Name ?? string.Empty)
			: await schoolIdentity.ReadAsync(NonEmpty(receipt.BoardId), NonEmpty(receipt.EducationMediumId),
				receipt.AcademicYearId, organization, cancellationToken);
		var savedLines = ReceiptCollectionSnapshot.Read(metadata);
		var lines = savedLines.Where(line => receipt.ReceiptScope == FeeReceiptScope.Combined ||
			(line.IsCharge ? receipt.ReceiptScope == FeeReceiptScope.Charge : receipt.ReceiptScope == FeeReceiptScope.Fee)).ToList();
		if (lines.Count == 0) lines = await LegacyLinesAsync(receipt, cancellationToken);
		var predecessor = receipt.LinkedProvisionalReceiptId.HasValue && receipts != null
			? await receipts.FindAsync(receipt.LinkedProvisionalReceiptId.Value, cancellationToken: cancellationToken) : null;
		var payments = paymentRecords == null ? [] : await paymentRecords.GetListAsync(
			payment => payment.FeeRecordMetadataId == receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		var standardId = account?.StandardId ?? student?.StandardId;
		var divisionId = account?.DivisionId ?? student?.DivisionId;
		var standard = standards != null && queryExecuter != null && standardId.HasValue
			? await queryExecuter.FirstOrDefaultAsync((await standards.GetQueryableAsync()).Where(item => item.Id == standardId).Select(item => item.Name), cancellationToken) : null;
		var division = divisions != null && queryExecuter != null && divisionId.HasValue
			? await queryExecuter.FirstOrDefaultAsync((await divisions.GetQueryableAsync()).Where(item => item.Id == divisionId).Select(item => item.Name), cancellationToken) : null;
		var family = families != null && student != null ? await families.FindAsync(student.Id, cancellationToken: cancellationToken) : student?.FamilyDetail;
		var guardian = family?.GuardianDetails?.FullName ?? family?.FatherDetails?.FullName ?? family?.MotherDetails?.FullName;
		var provisional = FeeReceiptPresentation.IsProvisional(receipt.ReceiptKind);
		var scopeTitle = receipt.ReceiptScope switch
		{
			FeeReceiptScope.Charge => "CHARGE RECEIPT", FeeReceiptScope.Combined => "FEES AND CHARGES RECEIPT", _ => "FEE RECEIPT"
		};
		return ClassicReceiptPdfRenderer.Render(new ReceiptDocumentModel
		{
			School = school, Title = (provisional ? "PROVISIONAL " : string.Empty) + scopeTitle,
			Number = receipt.ReceiptNumber, Date = receipt.ReceiptDate, AcademicYear = academicYear?.Name,
			StudentFields = [new(student == null ? "Applicant name" : "Student name", student == null ? account?.ApplicantName :
				string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(name => !string.IsNullOrWhiteSpace(name)))),
				new("GR No.", student?.GrNumber ?? student?.GrNo?.ToString(CultureInfo.InvariantCulture)),
				new("Class / section", string.Join(" - ", new[] { standard, division }.Where(value => !string.IsNullOrWhiteSpace(value)))),
				new("Parent / guardian", guardian), new("Roll no.", student?.RollNo?.ToString(CultureInfo.InvariantCulture))],
			PaymentFields = PaymentFields(payments), Lines = lines, Amount = (decimal)receipt.TotalPaidAmount,
			RelatedReceipt = predecessor?.ReceiptNumber,
			Status = receipt.VerificationStatus == FeeReceiptVerificationStatus.Rejected ? "Rejected" : null
		});
	}

	private static Guid? NonEmpty(Guid value) => value == Guid.Empty ? null : value;

	private async Task<List<ReceiptDocumentLine>> LegacyLinesAsync(FeePaymentReceipt receipt, CancellationToken cancellationToken)
	{
		var lines = new List<ReceiptDocumentLine>();
		var feeLines = allocations == null || receipt.ReceiptScope == FeeReceiptScope.Charge ? [] : await allocations.GetListAsync(
			line => line.FeeRecordMetadataId == receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		foreach (var line in feeLines.OrderBy(item => item.DueDate).ThenBy(item => item.FeeHeadName))
			lines.Add(new(line.FeeHeadName ?? "Fees", ClassicReceiptPdfRenderer.Period(line.CoverageStartDate, line.CoverageEndDate,
				line.InstallmentLabel), null, null, null, (decimal)line.Amount));
		// Older receipts did not preserve payable/paid-to-date/balance. Never fabricate those from today's dues.
		var feeTotal = receipt.ReceiptScope == FeeReceiptScope.Combined ? receipt.FeePaidAmount
			: receipt.ReceiptScope == FeeReceiptScope.Fee ? (decimal)receipt.TotalPaidAmount : 0m;
		var remainder = feeTotal - feeLines.Sum(line => (decimal)line.Amount);
		if (remainder > 0) lines.Add(new("Fees", "—", null, null, null, remainder));
		if (receipt.ReceiptScope is FeeReceiptScope.Charge or FeeReceiptScope.Combined)
		{
			var lateFees = receipt.LateFeePaidAmount ?? 0m;
			var charges = receipt.ChargePaidAmount - lateFees;
			if (lateFees > 0) lines.Add(new("Late fees", "—", null, null, null, lateFees, true));
			if (charges > 0) lines.Add(new("Charges", "—", null, null, null, charges, true));
		}
		return lines;
	}

	private static List<ReceiptDocumentField> PaymentFields(List<PaymentMethodRecord> payments)
	{
		if (payments.Count == 0) return [new("Payment mode", "Not recorded")];
		var fields = new List<ReceiptDocumentField>();
		foreach (var payment in payments.OrderBy(item => item.CreationTime))
		{
			fields.Add(new("Payment mode", FeeReceiptPresentation.GetPaymentMethodLabel(payment.PaymentMethod)));
			fields.Add(new("Payment date", payment.PaymentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)));
			var bank = payment.BankLabelSnapshot ?? (payment.PaymentDetails switch
			{
				BankTransferDetails transfer => transfer.BankName, ChequeDetails cheque => cheque.BankName,
				DemandDraftDetails draft => draft.IssuingBankName, CreditCardDetails card => card.BankName, _ => null
			});
			fields.Add(new("Bank", bank));
			var reference = payment.PaymentDetails switch
			{
				BankTransferDetails transfer => transfer.TransactionReferenceNumber, ChequeDetails cheque => cheque.ChequeNumber,
				DemandDraftDetails draft => draft.DDNumber, UpiDetails upi => upi.UpiTransactionId,
				CreditCardDetails card => card.TransactionId, OnlineGatewayDetails gateway => gateway.TransactionId,
				WalletDetails wallet => wallet.TransactionId, _ => null
			};
			if (!string.IsNullOrWhiteSpace(reference)) fields.Add(new("Transaction / instrument no.", reference));
			var instrumentDate = payment.PaymentDetails switch
			{
				BankTransferDetails transfer => transfer.TransferDate, ChequeDetails cheque => cheque.ChequeDate,
				DemandDraftDetails draft => draft.DDDate, _ => null
			};
			if (instrumentDate.HasValue) fields.Add(new("Instrument date", instrumentDate.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)));
			var holder = payment.PaymentDetails switch
			{
				BankTransferDetails transfer => transfer.AccountHolderName, CreditCardDetails card => card.CardHolderName, _ => null
			};
			if (!string.IsNullOrWhiteSpace(holder)) fields.Add(new("Account holder", holder));
		}
		return fields;
	}
}
