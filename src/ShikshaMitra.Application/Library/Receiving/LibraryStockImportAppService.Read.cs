using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Library.Receiving;

public partial class LibraryStockImportAppService
{
	public async Task<LibraryStockImportBatchDto> GetAsync(Guid id) => await ReadAsync(await RequireAccessAsync(false), id);

	public async Task<PaginatedResponse<LibraryStockImportSummaryDto>> GetListAsync(int page = 1, int pageSize = 25)
	{
		var query = await ScopedBatchesAsync(await RequireAccessAsync(false));
		var total = await AsyncExecuter.LongCountAsync(query);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var totalPages = (int)Math.Max(1, Math.Ceiling(total / (double)pageSize));
		page = Math.Clamp(page, 1, totalPages);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.ImportedAt).ThenByDescending(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		return new() { CurrentPage = page, PageSize = pageSize, TotalCount = total, TotalPages = totalPages, Items = items.Select(Summary).ToList() };
	}

	private async Task<LibraryStockImportBatchDto> ReadAsync(ImportActor actor, Guid id)
	{
		var batch = await AsyncExecuter.FirstOrDefaultAsync((await ScopedBatchesAsync(actor)).Where(row => row.Id == id))
			?? throw new EntityNotFoundException(typeof(LibraryStockImportBatch), id);
		var outcomes = await AsyncExecuter.ToListAsync((await rows.GetQueryableAsync())
			.Where(row => row.TenantId == actor.Tenant && row.BatchId == id).OrderBy(row => row.RowNumber)
			.Select(row => new LibraryStockImportRowDto(row.RowNumber, row.SourceRowId, row.BookId, row.Title,
				row.IsNewTitle, row.CopyId, row.ReceiptId, row.AccessionNumber)));
		return new(Summary(batch), outcomes);
	}

	private async Task<IQueryable<LibraryStockImportBatch>> ScopedBatchesAsync(ImportActor actor) =>
		(await batches.GetQueryableAsync()).Where(row => row.TenantId == actor.Tenant && Enumerable.Contains(actor.Years, row.AcademicYearId));

	private static LibraryStockImportSummaryDto Summary(LibraryStockImportBatch row) => new(row.Id, row.AcademicYearId,
		row.SectionId, row.ShelfId, row.ImportedBy, new DateTimeOffset(DateTime.SpecifyKind(row.ImportedAt, DateTimeKind.Utc)),
		row.RowCount, row.NewTitleCount, row.Source, row.SourceName);
}
