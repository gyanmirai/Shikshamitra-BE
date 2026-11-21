using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Shared;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.CampusTransfers;

namespace ShikshaMitra.Library.CampusTransfers;

public partial class LibraryCampusTransferAppService
{
	[Authorize(Actions.Dispatch)]
	public async Task<List<LibraryCampusTransferSection>> GetLocationsAsync() =>
		await locations.ListAsync((await RequireActorAsync(Actions.Dispatch)).Tenant);

	[Authorize(Actions.Dispatch)]
	public async Task<List<LibraryCampusTransferCopyOption>> FindCopiesAsync(Guid sourceSectionId, string search)
	{
		var actor = await RequireActorAsync(Actions.Dispatch);
		if (!await locations.CanDispatchFromAsync(actor.Tenant, sourceSectionId)) return [];
		return (await inventory.SearchAsync(actor.Tenant, search, sourceSectionId)).Select(MapStock).ToList();
	}

	public async Task<PaginatedResponse<LibraryCampusTransferListItem>> GetListAsync(int page = 1, int pageSize = 25)
	{
		var actor = await RequireActorAsync(Actions.View);
		pageSize = Math.Clamp(pageSize, 1, 100);
		page = Math.Clamp(page, 1, int.MaxValue / pageSize);
		var rows = (await transfers.GetQueryableAsync()).Where(row => row.TenantId == actor.Tenant);
		var count = await AsyncExecuter.CountAsync(rows);
		var visible = await AsyncExecuter.ToListAsync(rows.OrderByDescending(row => row.RecordedAt).ThenBy(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		var ids = visible.Select(row => row.Id).ToArray();
		var pending = await AsyncExecuter.ToListAsync((await items.GetQueryableAsync())
			.Where(row => row.TenantId == actor.Tenant && !row.ReceiptId.HasValue && Enumerable.Contains(ids, row.TransferId))
			.GroupBy(row => row.TransferId).Select(group => new { TransferId = group.Key, Count = group.Count() }));
		var byTransfer = pending.ToDictionary(row => row.TransferId, row => row.Count);
		return new() { TotalCount = count, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling((double)count / pageSize),
			Items = visible.Select(row => new LibraryCampusTransferListItem(Map(row), byTransfer.GetValueOrDefault(row.Id))).ToList() };
	}

	public async Task<LibraryCampusTransferDetail> GetAsync(Guid id)
	{
		var actor = await RequireActorAsync(Actions.View);
		var transfer = await FindTransferAsync(actor.Tenant, id);
		var allItems = await items.GetListAsync(row => row.TenantId == actor.Tenant && row.TransferId == id);
		var recorded = await receipts.GetListAsync(row => row.TenantId == actor.Tenant && row.TransferId == id);
		var byReceipt = allItems.ToLookup(row => row.ReceiptId);
		return new(Map(transfer), byReceipt[null].Count(), MapItems(allItems), recorded
			.OrderByDescending(row => row.RecordedAt).ThenBy(row => row.Id).Select(row => MapReceipt(row, byReceipt[row.Id])).ToList());
	}

	private static LibraryCampusTransferHeader Map(LibraryCampusTransfer row) => new(row.Id,
		new(row.SourceCampusId, row.SourceCampus, row.SourceSectionId, row.SourceSection),
		new(row.DestinationCampusId, row.DestinationCampus, row.DestinationSectionId, row.DestinationSection),
		new(row.DestinationShelfId, row.DestinationShelf), row.DispatchedOn, row.Reason, row.ActorId,
		AsUtcRecordedAt(row.RecordedAt), row.CopyCount);

	private static List<LibraryCampusTransferCopyDto> MapItems(IEnumerable<LibraryCampusTransferItem> rows) => rows
		.OrderBy(row => row.AccessionNumber).ThenBy(row => row.CopyId).Select(row => new LibraryCampusTransferCopyDto(
			row.CopyId, row.BookId, row.Title, row.AccessionNumber, row.SourceShelfId, row.ReceiptId, row.Condition)).ToList();

	private static LibraryCampusTransferReceiptDto MapReceipt(LibraryCampusTransferReceipt row, IEnumerable<LibraryCampusTransferItem> copies) =>
		new(row.Id, row.TransferId, row.ReceivedOn, row.Notes, row.ActorId, AsUtcRecordedAt(row.RecordedAt), MapItems(copies));

	// The legacy Npgsql timestamp reader can materialize timestamptz as local time.
	private static DateTime AsUtcRecordedAt(DateTime timestamp) => timestamp.Kind == DateTimeKind.Local
		? timestamp.ToUniversalTime()
		: DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
}
