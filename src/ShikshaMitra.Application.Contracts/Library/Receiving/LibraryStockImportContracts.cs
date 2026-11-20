using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Receiving;

public interface ILibraryStockImportAppService : IApplicationService
{
	Task<LibraryStockImportOptionsDto> GetOptionsAsync(Guid academicYearId);
	Task<LibraryStockImportPreviewDto> PreviewAsync(LibraryStockImportInput input);
	Task<LibraryStockImportBatchDto> CommitAsync(LibraryCommitStockImportCommand command);
	Task<LibraryStockImportBatchDto> GetAsync(Guid id);
	Task<PaginatedResponse<LibraryStockImportSummaryDto>> GetListAsync(int page = 1, int pageSize = 25);
}

public sealed record LibraryStockImportInput(string Csv, Guid AcademicYearId, Guid SectionId, Guid ShelfId,
	Guid? CategoryId, DateOnly ReceivedOn, LibraryStockSource? Source, string? SourceName)
{
	public Dictionary<string, string>? ColumnMap { get; init; }
}

public sealed record LibraryCommitStockImportCommand(LibraryStockImportInput Import, string Revision, string IdempotencyKey);
public sealed record LibraryStockImportErrorDto(int? RowNumber, string Field, string Code, string Message);
public sealed record LibraryStockImportPreviewRowDto(int RowNumber, string SourceRowId, Guid? BookId, string? TitleKey,
	string Title, string? Accession, DateOnly ReceivedOn, string? CataloguePrice, string? UnitCost, string? Currency);

public sealed class LibraryStockImportPreviewDto
{
	public string? Revision { get; set; }
	public int NewTitleCount { get; set; }
	public List<LibraryStockImportErrorDto> Errors { get; set; } = [];
	public List<LibraryStockImportPreviewRowDto> Rows { get; set; } = [];
}

public sealed record LibraryStockImportCategoryDto(Guid Id, string Name);
public sealed record LibraryStockImportYearDto(Guid Id, string Name);
public sealed record LibraryStockImportOptionsDto(bool CanCreateTitles, List<LibraryCopyMoveSectionDto> Sections,
	List<LibraryStockImportCategoryDto> Categories, List<LibraryStockImportYearDto> Years,
	LibraryCatalogueConfigurationDto? CatalogueConfiguration);

public sealed record LibraryStockImportSummaryDto(Guid Id, Guid AcademicYearId, Guid SectionId, Guid ShelfId,
	Guid ImportedBy, DateTimeOffset ImportedAt,
	int RowCount, int NewTitleCount, LibraryStockSource Source, string? SourceName);

public sealed record LibraryStockImportRowDto(int RowNumber, string SourceRowId, Guid BookId, string Title, bool IsNewTitle,
	Guid CopyId, Guid ReceiptId, string AccessionNumber);

public sealed record LibraryStockImportBatchDto(LibraryStockImportSummaryDto Batch, List<LibraryStockImportRowDto> Rows)
{
	public Guid Id => Batch.Id;
}
