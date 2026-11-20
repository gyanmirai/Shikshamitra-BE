using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Library.Receiving;

public partial class LibraryReceivingAppService
{
	public async Task<LibraryStockReceiptDto> GetAsync(Guid id)
	{
		await EnsureAccessAsync(false);
		return await ReadAsync(id);
	}

	private async Task<LibraryStockReceiptDto> ReadAsync(Guid id)
	{
		// Project the stored header rather than returning a tracked pre-save object.
		// PostgreSQL persists microseconds; the first response and replay must agree.
		var result = await AsyncExecuter.FirstOrDefaultAsync((await receipts.GetQueryableAsync())
			.Where(row => row.Id == id && row.TenantId == CurrentTenant.Id).Select(receipt => new LibraryStockReceiptDto
			{
				Id = receipt.Id, ReceivedOn = receipt.ReceivedOn, Source = receipt.Source, SourceName = receipt.SourceName,
				Reference = receipt.Reference, RecordedBy = receipt.RecordedBy, RecordedAt = receipt.RecordedAt,
				CopyCount = receipt.ExpectedCopyCount
			}))
			?? throw new EntityNotFoundException(typeof(LibraryStockReceipt), id);
		var lines = await receiptLines.GetListAsync(row => row.ReceiptId == id && row.TenantId == CurrentTenant.Id);
		var lineIds = lines.Select(row => row.Id).ToArray();
		var links = await receivedCopies.GetListAsync(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(lineIds, row.ReceiptLineId));
		result.Lines = lines.OrderBy(line => line.LineNumber)
			.Select(line => MapLine(line, links.Where(link => link.ReceiptLineId == line.Id))).ToList();
		await currentLabels.PopulateAsync(result.Lines);
		return result;
	}

	private static LibraryStockReceiptLineDto MapLine(LibraryStockReceiptLine line, IEnumerable<LibraryReceivedCopy> links) => new()
	{
		Id = line.Id, LineNumber = line.LineNumber, BookId = line.BookId, SectionId = line.SectionId, ShelfId = line.ShelfId,
		UnitCost = LibraryReceivingInputRules.CostText(line.UnitCost), Currency = line.Currency,
		Copies = links.OrderBy(copy => copy.CopyNumber).ThenBy(copy => copy.CopyId).Select(copy => new LibraryReceivedCopyDto
			{ CopyId = copy.CopyId, CopyNumber = copy.CopyNumber, AccessionNumber = copy.AccessionNumber }).ToList()
	};
}
