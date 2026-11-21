using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	private async Task<bool> EnsureExternalEmployeeReceiptAccessibleAsync(FeePaymentReceipt receipt, CancellationToken cancellationToken)
	{
		var receipts = LazyServiceProvider.LazyGetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
		var collectedReceipt = receipt.LinkedProvisionalReceiptId.HasValue
			? await receipts.FindAsync(receipt.LinkedProvisionalReceiptId.Value, includeDetails: false, cancellationToken: cancellationToken)
			: receipt;
		var collectedReceiptId = collectedReceipt?.Id ?? receipt.Id;
		var commands = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>()
			.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.Operation == "Collect" &&
				row.ResultEntityType == "Receipt" && row.ResultEntityId == collectedReceiptId, cancellationToken: cancellationToken);
		var allocations = await LazyServiceProvider.LazyGetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
			.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
				cancellationToken: cancellationToken);
		var chargeIds = allocations.Select(row => row.ChargeId).Distinct().ToArray();
		var links = LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>();
		var allocatedExternal = await links.AnyAsync(row => row.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(chargeIds, row.FinanceChargeId), cancellationToken);
		if (commands.Count == 0 && !allocatedExternal) return false;
		if (commands.Count != 1 || allocations.Count != 1 || collectedReceipt == null || collectedReceipt.TenantId != CurrentTenant.Id ||
			receipt.FinanceFeeAccountId.HasValue || receipt.StudentId.HasValue)
			throw new AbpAuthorizationException("Receipt source access denied.");
		if (collectedReceipt.Id != receipt.Id && !IsMatchingVerifiedExternalFinal(receipt, collectedReceipt))
			throw new AbpAuthorizationException("Final receipt does not match its verified original document.");
		var link = await links.FindAsync(commands[0].SourceLinkId, includeDetails: false, cancellationToken: cancellationToken);
		if (link == null || link.TenantId != CurrentTenant.Id || link.SourceKind != FinanceExternalSourceKind.LibraryAssessment)
			throw new AbpAuthorizationException("Receipt source access denied.");
		FinanceExternalSettlementContext context;
		try
		{
			context = await LazyServiceProvider.LazyGetRequiredService<FinanceExternalSettlementAuthority>().RequireScopeAsync(new()
			{
				Kind = link.SourceKind, SourceId = link.SourceId, SourceVersion = link.SourceVersion
			});
		}
		catch (BusinessException error) when (error.Code == UserScopeErrorCodes.AccessDenied)
		{
			throw new AbpAuthorizationException("Receipt source access denied.");
		}
		var header = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRecordMetadata, Guid>>()
			.FindAsync(receipt.FeeCollectionTransactionId, includeDetails: false, cancellationToken: cancellationToken);
		var allocation = allocations[0];
		if (context.Link.Id != link.Id || header == null || header.TenantId != CurrentTenant.Id ||
			collectedReceipt.FeeCollectionTransactionId != header.Id || header.ReceiptNumber != collectedReceipt.ReceiptNumber ||
			header.IdempotencyKey != commands[0].IdempotencyKey || header.CommandFingerprint != commands[0].CommandFingerprint ||
			header.AcademicYearId != link.AcademicYearId || receipt.AcademicYearId != link.AcademicYearId ||
			header.StudentId.HasValue || header.FinanceFeeAccountId.HasValue ||
			receipt.EmployeeId != context.Source.Payer.EmployeeId || header.EmployeeId != context.Source.Payer.EmployeeId ||
			allocation.EmployeeId != context.Source.Payer.EmployeeId || allocation.StudentId.HasValue ||
			allocation.ChargeId != context.Charge.Id || allocation.Amount != header.TotalPaidFee ||
			receipt.TotalPaidAmount != header.TotalPaidFee || receipt.Currency != link.Currency || header.Currency != link.Currency)
			throw new AbpAuthorizationException("Receipt source evidence does not match its recorded collection.");
		return true;
	}

	private static bool IsMatchingVerifiedExternalFinal(FeePaymentReceipt final, FeePaymentReceipt provisional) =>
		final.ReceiptKind == FeeReceiptKind.FinalCharge && provisional.ReceiptKind == FeeReceiptKind.ProvisionalCharge &&
		provisional.VerificationStatus == FeeReceiptVerificationStatus.Verified &&
		final.LinkedProvisionalReceiptId == provisional.Id && provisional.LinkedFinalReceiptId == final.Id &&
		final.TenantId == provisional.TenantId && final.AcademicYearId == provisional.AcademicYearId &&
		final.FeeCollectionTransactionId == provisional.FeeCollectionTransactionId &&
		final.StudentId == provisional.StudentId && final.EmployeeId == provisional.EmployeeId &&
		final.FinanceFeeAccountId == provisional.FinanceFeeAccountId && final.Currency == provisional.Currency &&
		final.ReceiptScope == provisional.ReceiptScope && final.ReceiptDate == provisional.ReceiptDate &&
		final.TotalPaidAmount == provisional.TotalPaidAmount;
}
