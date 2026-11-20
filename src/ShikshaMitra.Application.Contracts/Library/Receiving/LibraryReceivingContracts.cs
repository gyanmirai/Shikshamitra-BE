using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Receiving;

public interface ILibraryReceivingAppService : IApplicationService
{
	Task<LibraryReceivingOptionsDto> GetOptionsAsync();
	Task<LibraryReceivingPreviewDto> PreviewAsync(LibraryReceivingInput input);
	Task<LibraryStockReceiptDto> ReceiveAsync(LibraryReceiveCommand input);
	Task<LibraryStockReceiptDto> GetAsync(Guid id);
	Task<ShikshaMitra.Library.Shared.PaginatedResponse<LibraryStockReceiptSummaryDto>> GetListAsync(LibraryReceivingListInput input);
}

public sealed class LibraryReceivingOptionsDto
{
	public List<ShikshaMitra.Library.Books.LibraryCopyMoveSectionDto> Sections { get; set; } = [];
}

public sealed class LibraryReceivingInput
{
	public DateOnly ReceivedOn { get; set; }
	public LibraryStockSource? Source { get; set; }
	public string? SourceName { get; set; }
	public string? Reference { get; set; }
	public List<LibraryReceivingLineInput> Lines { get; set; } = [];
}

public sealed class LibraryReceivingLineInput
{
	public Guid BookId { get; set; }
	public Guid SectionId { get; set; }
	public Guid ShelfId { get; set; }
	public int Quantity { get; set; }
	public string? UnitCost { get; set; }
	public string? Currency { get; set; }
	public List<string>? Accessions { get; set; }
}

public sealed class LibraryReceiveCommand
{
	public string IdempotencyKey { get; set; } = string.Empty;
	public string Revision { get; set; } = string.Empty;
	public LibraryReceivingInput Receipt { get; set; } = new();
}

public sealed class LibraryReceivingPreviewDto
{
	public bool CanReceive { get; set; }
	public string? Revision { get; set; }
	public List<LibraryReceivingErrorDto> Errors { get; set; } = [];
	public List<LibraryReceivingLinePreviewDto> Lines { get; set; } = [];
	public int KnownCostCopyCount { get; set; }
	public int UnknownCostCopyCount { get; set; }
	public List<LibraryReceivingCurrencyTotalDto> KnownTotals { get; set; } = [];
}

public sealed class LibraryReceivingErrorDto
{
	public int? LineIndex { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public sealed class LibraryReceivingLinePreviewDto
{
	public int LineIndex { get; set; }
	public Guid BookId { get; set; }
	public string BookTitle { get; set; } = string.Empty;
	public Guid SectionId { get; set; }
	public string SectionName { get; set; } = string.Empty;
	public Guid ShelfId { get; set; }
	public string ShelfName { get; set; } = string.Empty;
	public int Quantity { get; set; }
	public string? UnitCost { get; set; }
	public string? Currency { get; set; }
}

public sealed class LibraryReceivingCurrencyTotalDto
{
	public string Currency { get; set; } = string.Empty;
	public string Amount { get; set; } = string.Empty;
}
