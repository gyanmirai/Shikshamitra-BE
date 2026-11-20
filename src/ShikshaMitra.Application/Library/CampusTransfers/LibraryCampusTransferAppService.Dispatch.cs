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
	[Authorize(Actions.Dispatch)]
	public async Task<LibraryCampusTransferPreview> PreviewDispatchAsync(LibraryCampusDispatchInput input)
	{
		var actor = await RequireActorAsync(Actions.Dispatch);
		var normalized = NormalizeDispatch(input);
		return (await BuildDispatchAsync(actor.Tenant, normalized, Hash(normalized))).Preview;
	}

	[Authorize(Actions.Dispatch)]
	[UnitOfWork(true)]
	public async Task<LibraryCampusTransferHeader> DispatchAsync(LibraryCampusDispatchCommand command)
	{
		var actor = await RequireActorAsync(Actions.Dispatch);
		RequireCommand(command?.Revision, command?.IdempotencyKey);
		var input = NormalizeDispatch(command!.Dispatch);
		var fingerprint = Hash(input);
		await inventoryLock.AcquireAsync(actor.Tenant);
		var existing = await transfers.FindAsync(row => row.TenantId == actor.Tenant && row.ActorId == actor.User &&
			row.IdempotencyKey == command.IdempotencyKey);
		if (existing != null)
		{
			RequireExactRetry(existing.PayloadFingerprint, fingerprint);
			return Map(existing);
		}
		var plan = await BuildDispatchAsync(actor.Tenant, input, fingerprint);
		RequirePreview(plan.Preview, command.Revision);
		var route = plan.Route!;
		var transfer = new LibraryCampusTransfer(GuidGenerator.Create(), actor.Tenant, route.Source.Snapshot(), route.Destination.Snapshot(),
			route.ShelfId, route.Shelf, input.DispatchedOn, input.Reason, actor.User, RecordedAt(), plan.Stock.Count,
			command.IdempotencyKey, fingerprint);
		var assigned = new List<LibraryCampusTransferItem>(plan.Stock.Count);
		foreach (var row in plan.Stock.Values.OrderBy(row => row.Copy.Id))
		{
			var copy = row.Copy;
			var before = LibraryCopySnapshot.Capture(copy);
			copy.Status = BookCopyStatus.InTransit;
			await copies.UpdateAsync(copy);
			var evidence = await recorder.RecordWithResultAsync(before, copy, LibraryCopyEventType.TransferDispatched,
				LibraryOperationActor.User(actor.User), input.Reason);
			assigned.Add(new(GuidGenerator.Create(), actor.Tenant, transfer.Id, copy, row.Title, evidence.Id));
		}
		await transfers.InsertAsync(transfer);
		await items.InsertManyAsync(assigned);
		await units.Current!.SaveChangesAsync();
		return Map(transfer);
	}

	private async Task<DispatchPlan> BuildDispatchAsync(Guid tenant, LibraryCampusDispatchInput input, string fingerprint)
	{
		var route = await locations.ForDispatchAsync(tenant, input);
		if (route == null) return new(new(null, [], [new(null, "InvalidRoute",
			"Choose real sections at two active campuses and a shelf at the destination.")]), null, []);
		var stock = await inventory.LoadAsync(tenant, input.CopyIds);
		var conflicts = await inventory.FindUnavailableAsync(tenant, stock, handingOff: true);
		var errors = new List<LibraryCampusTransferError>();
		foreach (var id in input.CopyIds)
		{
			if (!stock.TryGetValue(id, out var row) || row.Copy.SectionId != input.SourceSectionId ||
				row.Copy.Status != BookCopyStatus.Available || conflicts.Contains(id))
				errors.Add(new(id, "CopyUnavailable", "Choose an available copy at the source section with no active obligation."));
		}
		var selected = stock.Values.Where(row => row.Copy.SectionId == input.SourceSectionId).OrderBy(row => row.Copy.Id).ToArray();
		var revision = errors.Count == 0 ? Hash(new { fingerprint, route, Stock = selected.Select(StockEvidence).ToArray() }) : null;
		return new(new(revision, selected.Select(row => MapStock(row.Label)).ToList(), errors), route, stock);
	}

	private static LibraryCampusDispatchInput NormalizeDispatch(LibraryCampusDispatchInput? input)
	{
		var reason = input?.Reason?.Trim();
		if (input == null || input.SourceSectionId == Guid.Empty || input.DestinationSectionId == Guid.Empty ||
			input.SourceSectionId == input.DestinationSectionId || input.DestinationShelfId == Guid.Empty ||
			input.DispatchedOn == default || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 ||
			reason.Contains('\0') || !ValidCopies(input.CopyIds))
			throw Invalid();
		return input with { Reason = reason, CopyIds = input.CopyIds.Order().ToArray() };
	}

	private sealed record DispatchPlan(LibraryCampusTransferPreview Preview,
		LibraryCampusTransferLocations.LibraryCampusTransferRoute? Route, Dictionary<Guid, LibraryCustodyStock> Stock);
}
