using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Domain.Entities;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;

namespace ShikshaMitra.Library.Custody;

public partial class LibraryCustodyAppService
{
	public async Task<LibraryCustodyDetailDto> GetAsync(Guid id)
	{
		var actor = await RequireActorAsync(Actions.View);
		var handoff = await ReadHandoffAsync(actor.Tenant, id);
		var inventoryRows = await ReadItemsAsync(actor.Tenant, id);
		var returning = inventoryRows.ToLookup(row => row.ReturnReceiptId);
		var history = await AsyncExecuter.ToListAsync((await receipts.GetQueryableAsync())
			.Where(row => row.TenantId == actor.Tenant && row.HandoffId == id).OrderBy(row => row.RecordedAt).ThenBy(row => row.Id));
		return new(Map(handoff), returning[null].Count(), inventoryRows, history.Select(row => MapReceipt(row, returning[row.Id])).ToList());
	}

	public async Task<PaginatedResponse<LibraryCustodyListItemDto>> GetListAsync(int page = 1, int pageSize = 25)
	{
		var actor = await RequireActorAsync(Actions.View);
		var query = (await handoffs.GetQueryableAsync()).Where(row => row.TenantId == actor.Tenant);
		var total = await AsyncExecuter.LongCountAsync(query);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var pages = (int)Math.Max(1, Math.Ceiling(total / (double)pageSize));
		page = Math.Clamp(page, 1, pages);
		var itemQuery = (await items.GetQueryableAsync()).Where(row => row.TenantId == actor.Tenant && !row.ReturnReceiptId.HasValue);
		var results = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.RecordedAt).ThenByDescending(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize).Select(row => new { Handoff = row, Pending = itemQuery.Count(item => item.HandoffId == row.Id) }));
		return new() { CurrentPage = page, PageSize = pageSize, TotalPages = pages, TotalCount = total,
			Items = results.Select(row => new LibraryCustodyListItemDto(Map(row.Handoff), row.Pending)).ToList() };
	}

	private async Task<LibraryCustodyHandoff> ReadHandoffAsync(Guid tenant, Guid id) =>
		await handoffs.FindAsync(row => row.TenantId == tenant && row.Id == id) ?? throw new EntityNotFoundException(typeof(LibraryCustodyHandoff), id);

	private async Task<List<LibraryCustodyItemDto>> ReadItemsAsync(Guid tenant, Guid id, Guid? receiptId = null)
	{
		var query = (await items.GetQueryableAsync()).Where(row => row.TenantId == tenant && row.HandoffId == id);
		if (receiptId.HasValue) query = query.Where(row => row.ReturnReceiptId == receiptId);
		return await AsyncExecuter.ToListAsync(query.OrderBy(row => row.AccessionNumber).ThenBy(row => row.CopyId)
			.Select(row => new LibraryCustodyItemDto(row.CopyId, row.BookId, row.Title, row.AccessionNumber,
				row.ReturnReceiptId, row.ReturnedCondition, row.HomeSectionId, row.HomeShelfId)));
	}

	private async Task<LibraryCustodyReceiptDto> ReadReceiptAsync(Guid tenant, LibraryCustodyReceipt receipt) =>
		MapReceipt(receipt, await ReadItemsAsync(tenant, receipt.HandoffId, receipt.Id));

	private static LibraryCustodyReceiptDto MapReceipt(LibraryCustodyReceipt row, IEnumerable<LibraryCustodyItemDto> copies) =>
		new(row.Id, row.HandoffId, row.ReceivedOn, row.Notes, row.ActorId, ToOffset(row.RecordedAt), copies.ToList());

	private static LibraryCustodyHandoffDto Map(LibraryCustodyHandoff row) => new(row.Id, row.AcademicYearId, row.EmployeeId, row.Custodian,
		row.StandardId, row.ClassName, row.RoomId, row.RoomName, row.Purpose, row.HandedOffOn, row.ExpectedReturnOn,
		row.ActorId, ToOffset(row.RecordedAt), row.CopyCount);

	private static DateTimeOffset ToOffset(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));
}
