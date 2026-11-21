using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

/// <summary>Scopes existing external cash instruments through their recorded collection source.</summary>
public class FinanceExternalCashSourceScope(
	IRepository<FinanceExternalReceivableCommand, Guid> commands,
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	IRepository<FeePaymentReceipt, Guid> receipts,
	IRepository<FeeRecordMetadata, Guid> collections,
	IRepository<ChargeCollectionRecord, Guid> allocations,
	IRepository<PaymentMethodRecord, Guid> instruments,
	IRepository<Charge, Guid> charges,
	IFinanceExternalSourceScope sourceScope,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<IQueryable<CashCollection>> ApplyAsync(IQueryable<CashCollection> cash,
		IQueryable<Guid> legacyStudentIds, IQueryable<Guid> legacyAccountIds, bool manualAccessible)
	{
		var sourceLinks = (await links.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var collectCommands = (await commands.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
			row.Operation == "Collect" && row.ResultEntityType == "Receipt");
		var receiptQuery = (await receipts.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var collectionQuery = (await collections.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var allocationQuery = (await allocations.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var instrumentQuery = (await instruments.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var chargeQuery = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var sources = await sourceScope.GetReadableSourcesAsync(FinanceExternalSourceKind.LibraryAssessment);

		// The immutable Collect result identifies an external receipt. Allocation
		// lineage also prevents a known external header from falling through to a
		// legacy current-student/manual rule when another evidence edge is invalid.
		var recordedMetadataIds = from command in collectCommands
			join link in sourceLinks on command.SourceLinkId equals link.Id
			join receipt in receiptQuery on command.ResultEntityId equals (Guid?)receipt.Id
			select receipt.FeeCollectionTransactionId;
		var allocatedMetadataIds = allocationQuery.Where(allocation =>
			sourceLinks.Any(link => link.FinanceChargeId == allocation.ChargeId))
			.Select(allocation => allocation.FeeRecordMetadataId);
		var externalMetadataIds = recordedMetadataIds.Union(allocatedMetadataIds);

		var readableCashIds = from row in cash
			join header in collectionQuery on row.SourceId equals header.Id
			join instrument in instrumentQuery on row.PaymentInstrumentId equals (Guid?)instrument.Id
			join receipt in receiptQuery on header.Id equals receipt.FeeCollectionTransactionId
			join command in collectCommands on (Guid?)receipt.Id equals command.ResultEntityId
			join link in sourceLinks on command.SourceLinkId equals link.Id
			join source in sources on link.SourceId equals source.SourceId
			join charge in chargeQuery on link.FinanceChargeId equals charge.Id
			where link.SourceKind == FinanceExternalSourceKind.LibraryAssessment &&
				row.TenantId == tenant.Id && row.AcademicYearId == header.AcademicYearId &&
				row.AcademicYearId == receipt.AcademicYearId && row.AcademicYearId == link.AcademicYearId &&
				row.AcademicYearId == charge.AcademicYearId && row.AcademicYearId == source.AcademicYearId &&
				charge.ChargeRequestId == link.FinanceChargeRequestId &&
				header.Currency == link.Currency && receipt.Currency == link.Currency &&
				!row.FinanceFeeAccountId.HasValue && !header.FinanceFeeAccountId.HasValue &&
				!instrument.FinanceFeeAccountId.HasValue && !receipt.FinanceFeeAccountId.HasValue &&
				row.StudentId == source.StudentId && row.EmployeeId == source.EmployeeId &&
				header.StudentId == source.StudentId && header.EmployeeId == source.EmployeeId &&
				instrument.StudentId == source.StudentId && instrument.EmployeeId == source.EmployeeId &&
				receipt.StudentId == source.StudentId && receipt.EmployeeId == source.EmployeeId &&
				charge.StudentId == source.StudentId && charge.EmployeeId == source.EmployeeId &&
				instrument.FeeRecordMetadataId == header.Id && instrument.PaymentMethod == PaymentMethodEnum.Cash &&
				instrument.Amount == row.Amount && receipt.TotalPaidAmount == header.TotalPaidFee &&
				allocationQuery.Count(allocation => allocation.FeeRecordMetadataId == header.Id) == 1 &&
				allocationQuery.Any(allocation => allocation.FeeRecordMetadataId == header.Id &&
					allocation.ChargeId == charge.Id && allocation.StudentId == source.StudentId &&
					allocation.EmployeeId == source.EmployeeId && allocation.Amount == header.TotalPaidFee)
			select row.Id;

		return cash.Where(row => row.EmployeeId.HasValue || externalMetadataIds.Any(id => id == row.SourceId)
			? readableCashIds.Any(id => id == row.Id)
			: row.FinanceFeeAccountId.HasValue ? legacyAccountIds.Contains(row.FinanceFeeAccountId.Value)
			: row.StudentId.HasValue ? legacyStudentIds.Contains(row.StudentId.Value) : manualAccessible);
	}
}
