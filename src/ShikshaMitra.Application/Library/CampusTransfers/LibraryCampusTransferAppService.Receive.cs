using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.CampusTransfers;

namespace ShikshaMitra.Library.CampusTransfers;

public partial class LibraryCampusTransferAppService
{
	[Authorize(Actions.Receive)]
	public async Task<LibraryCampusTransferPreview> PreviewReceiveAsync(Guid id, LibraryCampusReceiveInput input)
	{
		var actor = await RequireActorAsync(Actions.Receive);
		var transfer = await FindTransferAsync(actor.Tenant, id);
		var normalized = NormalizeReceive(input);
		return (await BuildReceiptAsync(actor.Tenant, transfer, normalized, Hash(normalized))).Preview;
	}

	[Authorize(Actions.Receive)]
	[UnitOfWork(true)]
	public async Task<LibraryCampusTransferReceiptDto> ReceiveAsync(Guid id, LibraryCampusReceiveCommand command)
	{
		var actor = await RequireActorAsync(Actions.Receive);
		RequireCommand(command?.Revision, command?.IdempotencyKey);
		var input = NormalizeReceive(command!.Receipt);
		var fingerprint = Hash(input);
		await inventoryLock.AcquireAsync(actor.Tenant);
		var transfer = await FindTransferAsync(actor.Tenant, id);
		var existing = await receipts.FindAsync(row => row.TenantId == actor.Tenant && row.TransferId == id &&
			row.ActorId == actor.User && row.IdempotencyKey == command.IdempotencyKey);
		if (existing != null)
		{
			RequireExactRetry(existing.PayloadFingerprint, fingerprint);
			return MapReceipt(existing, await items.GetListAsync(row => row.TenantId == actor.Tenant &&
				row.TransferId == id && row.ReceiptId == existing.Id));
		}
		var plan = await BuildReceiptAsync(actor.Tenant, transfer, input, fingerprint);
		RequirePreview(plan.Preview, command.Revision);
		var receipt = new LibraryCampusTransferReceipt(GuidGenerator.Create(), actor.Tenant, id, input.ReceivedOn, input.Notes,
			actor.User, RecordedAt(), input.Copies.Length, command.IdempotencyKey, fingerprint);
		var byCopy = plan.Assigned.ToDictionary(row => row.CopyId);
		var reason = input.Notes ?? transfer.Reason;
		foreach (var line in input.Copies)
		{
			var copy = plan.Stock[line.CopyId].Copy;
			var before = LibraryCopySnapshot.Capture(copy);
			copy.Status = line.Condition;
			copy.SectionId = transfer.DestinationSectionId;
			copy.ShelfId = transfer.DestinationShelfId;
			await copies.UpdateAsync(copy);
			var evidence = await recorder.RecordWithResultAsync(before, copy, LibraryCopyEventType.TransferReceived,
				LibraryOperationActor.User(actor.User), reason);
			byCopy[copy.Id].Receive(receipt.Id, evidence.Id, line.Condition);
		}
		await receipts.InsertAsync(receipt);
		await items.UpdateManyAsync(plan.Assigned);
		await units.Current!.SaveChangesAsync();
		foreach (var bookId in plan.Stock.Values.Where(row => row.Copy.Status == BookCopyStatus.Available).Select(row => row.Copy.BookId).Distinct().Order())
			await reservations.RebalanceAsync(bookId);
		return MapReceipt(receipt, plan.Assigned);
	}

	private async Task<ReceiptPlan> BuildReceiptAsync(Guid tenant, LibraryCampusTransfer transfer, LibraryCampusReceiveInput input, string fingerprint)
	{
		var errors = new List<LibraryCampusTransferError>();
		if (input.ReceivedOn < transfer.DispatchedOn)
			errors.Add(new(null, "ReceiptDate", "Receive on or after the original dispatch date."));
		if (!await locations.CanReceiveAsync(tenant, transfer))
			errors.Add(new(null, "DestinationUnavailable", "The recorded destination shelf is unavailable."));
		var ids = input.Copies.Select(row => row.CopyId).ToArray();
		var assigned = await items.GetListAsync(row => row.TenantId == tenant && row.TransferId == transfer.Id && Enumerable.Contains(ids, row.CopyId));
		var byCopy = assigned.ToDictionary(row => row.CopyId);
		var stock = await inventory.LoadAsync(tenant, assigned.Select(row => row.CopyId).ToArray());
		var conflicts = await inventory.FindUnavailableAsync(tenant, stock, handingOff: false);
		foreach (var id in ids)
		{
			if (!byCopy.TryGetValue(id, out var item) || item.ReceiptId.HasValue || !stock.TryGetValue(id, out var row) ||
				row.Copy.Status != BookCopyStatus.InTransit || row.Copy.SectionId != transfer.SourceSectionId ||
				row.Copy.ShelfId != item.SourceShelfId || row.Copy.BookId != item.BookId ||
				row.Copy.AccessionNumber != item.AccessionNumber || conflicts.Contains(id))
				errors.Add(new(id, "CopyUnavailable", "Receive only a copy that is still outstanding on this transfer."));
		}
		var selected = stock.Values.OrderBy(row => row.Copy.Id).ToArray();
		var revision = errors.Count == 0 ? Hash(new { transfer.Id, fingerprint, Stock = selected.Select(StockEvidence).ToArray() }) : null;
		return new(new(revision, selected.Select(row => MapStock(row.Label)).ToList(), errors), assigned, stock);
	}

	private static LibraryCampusReceiveInput NormalizeReceive(LibraryCampusReceiveInput? input)
	{
		var notes = input?.Notes?.Trim();
		if (input == null || input.ReceivedOn == default || notes?.Length > 500 || notes?.Contains('\0') == true ||
			!ValidCopies(input.Copies?.Select(row => row?.CopyId ?? Guid.Empty).ToArray()) ||
			input.Copies!.Any(row => !LibraryCampusTransferItem.CanReceiveCondition(row.Condition))) throw Invalid();
		return input with { Notes = string.IsNullOrEmpty(notes) ? null : notes,
			Copies = input.Copies!.OrderBy(row => row.CopyId).ToArray() };
	}

	private sealed record ReceiptPlan(LibraryCampusTransferPreview Preview, List<LibraryCampusTransferItem> Assigned,
		Dictionary<Guid, LibraryCustodyStock> Stock);
}
