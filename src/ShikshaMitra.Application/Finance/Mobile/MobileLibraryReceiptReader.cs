using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Mobile;

[RemoteService(false)]
public class MobileLibraryReceiptReader(MobileFinanceAccessService finance,
	IRepository<FinanceExternalReceivableSourceLink, Guid> sources, IRepository<ChargeCollectionRecord, Guid> allocations,
	IFeePaymentReceiptRepository receipts, IRepository<FeeRecordMetadata, Guid> transactions) : ApplicationService, ITransientDependency
{
	internal async Task<List<LibraryPortalReceiptYear>> ListYearsAsync(Guid student)
	{
		var owner = await finance.RequireAsync(student, permission: MobileFinancePermissions.ViewReceipts);
		return owner.AcademicYears.Select(year => new LibraryPortalReceiptYear(year.Id, year.Name)).ToList();
	}

	internal async Task<PaginatedResponse<LibraryPortalReceiptDto>> ListAsync(Guid student, Guid year, int page, int size)
	{
		await finance.RequireAsync(student, year, permission: MobileFinancePermissions.ViewReceipts);
		var tenant = CurrentTenant.Id;
		var schoolSources = (await sources.GetQueryableAsync()).Where(source => source.TenantId == tenant &&
			source.AcademicYearId == year && source.SourceKind == FinanceExternalSourceKind.LibraryAssessment);
		var paidSources = from allocation in await allocations.GetQueryableAsync()
			join source in schoolSources on allocation.ChargeId equals source.FinanceChargeId
			where allocation.TenantId == tenant && allocation.StudentId == student && !allocation.EmployeeId.HasValue
			select allocation.FeeRecordMetadataId;
		var ownTransactions = (await transactions.GetQueryableAsync()).Where(transaction =>
			transaction.TenantId == tenant && transaction.StudentId == student && transaction.AcademicYearId == year);
		var eligible = from receipt in await receipts.GetQueryableAsync()
			join transaction in ownTransactions on receipt.FeeCollectionTransactionId equals transaction.Id
			where receipt.TenantId == tenant && receipt.StudentId == student && receipt.AcademicYearId == year &&
				!receipt.EmployeeId.HasValue && (receipt.ReceiptScope == FeeReceiptScope.Charge || receipt.ReceiptScope == FeeReceiptScope.Combined) &&
				paidSources.Contains(transaction.Id)
			select receipt;
		var count = await AsyncExecuter.CountAsync(eligible);
		var rows = await AsyncExecuter.ToListAsync(eligible.OrderByDescending(row => row.ReceiptDate).ThenBy(row => row.Id)
			.Skip((page - 1) * size).Take(size)
			.Select(row => new { row.Id, row.ReceiptNumber, row.ReceiptDate, row.ReceiptKind, row.VerificationStatus }));
		return new() { CurrentPage = page, PageSize = size, TotalCount = count, TotalPages = (int)Math.Ceiling((double)count / size),
			Items = rows.Select(row => new LibraryPortalReceiptDto(row.Id, row.ReceiptNumber, row.ReceiptDate, row.ReceiptKind.ToString(),
				row.VerificationStatus.ToString())).ToList() };
	}
}
