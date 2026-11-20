using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

public partial class LibraryReceivingAppService
{
	public async Task<PaginatedResponse<LibraryStockReceiptSummaryDto>> GetListAsync(LibraryReceivingListInput input)
	{
		await EnsureAccessAsync(false);
		if (input == null || input.Source.HasValue && !Enum.IsDefined(input.Source.Value) ||
			input.ReceivedFrom.HasValue && input.ReceivedTo.HasValue && input.ReceivedFrom > input.ReceivedTo ||
			input.Search?.Length > 256)
			throw new BusinessException(LibraryErrorCodes.ReceivingInvalid);
		var page = Math.Max(1, input.Page);
		var size = Math.Clamp(input.PageSize, 1, 100);
		var query = (await receipts.GetQueryableAsync()).Where(receipt => receipt.TenantId == CurrentTenant.Id);
		if (input.Source.HasValue) query = query.Where(receipt => receipt.Source == input.Source.Value);
		if (input.ReceivedFrom.HasValue) query = query.Where(receipt => receipt.ReceivedOn >= input.ReceivedFrom.Value);
		if (input.ReceivedTo.HasValue) query = query.Where(receipt => receipt.ReceivedOn <= input.ReceivedTo.Value);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(receipt => receipt.SourceName != null && receipt.SourceName.ToLower().Contains(search) ||
				receipt.Reference != null && receipt.Reference.ToLower().Contains(search));
		}
		if (input.CopyId.HasValue)
		{
			var lines = (await receiptLines.GetQueryableAsync()).Where(line => line.TenantId == CurrentTenant.Id);
			var links = (await receivedCopies.GetQueryableAsync()).Where(link => link.TenantId == CurrentTenant.Id && link.CopyId == input.CopyId.Value);
			var matchingIds = from line in lines join link in links on line.Id equals link.ReceiptLineId select line.ReceiptId;
			query = query.Where(receipt => matchingIds.Contains(receipt.Id));
		}
		var count = await AsyncExecuter.CountAsync(query);
		var result = new PaginatedResponse<LibraryStockReceiptSummaryDto>
			{ CurrentPage = page, PageSize = size, TotalCount = count, TotalPages = (int)Math.Ceiling(count / (double)size) };
		var offset = ((long)page - 1) * size;
		if (offset > int.MaxValue || offset >= count) return result;
		result.Items = await AsyncExecuter.ToListAsync(query.OrderByDescending(receipt => receipt.ReceivedOn)
			.ThenByDescending(receipt => receipt.RecordedAt).ThenByDescending(receipt => receipt.Id).Skip((int)offset).Take(size)
			.Select(receipt => new LibraryStockReceiptSummaryDto
			{
				Id = receipt.Id, ReceivedOn = receipt.ReceivedOn, Source = receipt.Source, SourceName = receipt.SourceName,
				Reference = receipt.Reference, RecordedBy = receipt.RecordedBy, RecordedAt = receipt.RecordedAt,
				LineCount = receipt.ExpectedLineCount, CopyCount = receipt.ExpectedCopyCount
			}));
		return result;
	}
}
