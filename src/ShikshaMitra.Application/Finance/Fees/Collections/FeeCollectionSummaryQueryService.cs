using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public partial class FeeCollectionSummaryQueryService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<FinanceFeeAccount, Guid> accountRepository,
	IReadOnlyRepository<Standard, Guid> standardRepository,
	IReadOnlyRepository<Division, Guid> divisionRepository,
	IReadOnlyRepository<FeeHead, Guid> feeHeadRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<ChargeWaiverApplication, Guid> chargeWaiverRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<ConcessionRequest, Guid> concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService,
	LateFeeBalanceService lateFeeBalanceService,
	IClock clock
) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
        var tableQuery = ShikshaMitra.Finance.Common.FinanceTableQuery.Parse(input.Filters, null,
            input.SortBy, input.SortOrder, TableColumns);
		var allRows = await BuildRawRowsAsync(input, cancellationToken);
        // Keep the canonical balances/status mapping; apply header filters to those same values before counting or paging.
		var rawFiltered = ApplyStatusFilter(allRows, input.Status);
        var rows = MapToDto(ApplySorting(rawFiltered, input.Sorting).ThenBy(r => r.StudentId).ToList()).AsQueryable();
        var filtered = tableQuery.Apply(rows, TableFields);

		var totalCount = filtered.Count();
		if (totalCount == 0)
			return new PagedResultDto<FeeCollectionSummaryItemDto>(0, []);

        var ordered = tableQuery.SortBy is null ? filtered : tableQuery.Order(filtered, TableFields, "name", false, r => r.FinanceFeeAccountId ?? r.StudentId ?? Guid.Empty);
		var page = ordered
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		return new PagedResultDto<FeeCollectionSummaryItemDto>(totalCount, page);
	}

	public async Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default)
	{
		var filterInput = new GetFeeCollectionSummaryInput
		{
			AcademicYearId    = input.AcademicYearId,
			BoardId           = input.BoardId,
			EducationMediumId = input.EducationMediumId,
			StandardId        = input.StandardId,
			DivisionId        = input.DivisionId,
			Search            = input.Search,
			Status            = input.Status,
		};

		var allRows = await BuildRawRowsAsync(filterInput, cancellationToken);
		var filtered = ApplyStatusFilter(allRows, input.Status);
		var sorted   = ApplySorting(filtered, sorting: null).ToList();
		var rows     = MapToDto(sorted);

		return input.Format == FeeCollectionExportFormat.Excel
			? BuildExcel(rows)
			: BuildCsv(rows);
	}
}
