using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;

namespace ShikshaMitra.Library.Custody;

public partial class LibraryCustodyAppService
{
	[Authorize(Actions.Return)]
	public async Task<LibraryCustodyPreviewDto> PreviewReturnAsync(Guid id, LibraryCustodyReturnInput input)
	{
		var actor = await RequireActorAsync(Actions.Return);
		var handoff = await ReadHandoffAsync(actor.Tenant, id);
		var normalized = NormalizeReturn(input);
		return (await BuildReturnAsync(actor.Tenant, handoff, normalized, Hash(new { id, normalized }))).Preview;
	}

	[Authorize(Actions.Return)]
	[UnitOfWork(true)]
	public async Task<LibraryCustodyReceiptDto> ReturnAsync(Guid id, LibraryCustodyReturnCommand command)
	{
		var actor = await RequireActorAsync(Actions.Return);
		ValidateCommand(command?.Revision, command?.IdempotencyKey);
		var input = NormalizeReturn(command!.Return);
		await inventoryLock.AcquireAsync(actor.Tenant);
		var handoff = await ReadHandoffAsync(actor.Tenant, id);
		var fingerprint = Hash(new { id, normalized = input });
		var saved = await receipts.FindAsync(row => row.TenantId == actor.Tenant && row.ActorId == actor.User && row.IdempotencyKey == command.IdempotencyKey);
		if (saved != null)
		{
			RequireFingerprint(saved.PayloadFingerprint, fingerprint);
			return await ReadReceiptAsync(actor.Tenant, saved);
		}
		var plan = await BuildReturnAsync(actor.Tenant, handoff, input, fingerprint);
		RequirePreview(plan.Preview, command.Revision);
		var receipt = new LibraryCustodyReceipt(GuidGenerator.Create(), actor.Tenant, handoff.Id, input.ReceivedOn, input.Notes,
			actor.User, RecordedAt(), input.Copies.Length, command.IdempotencyKey, fingerprint);
		var events = new Dictionary<Guid, Guid>();
		foreach (var line in input.Copies)
		{
			var copy = plan.Stock[line.CopyId].Copy;
			var before = LibraryCopySnapshot.Capture(copy);
			copy.Status = line.Condition;
			await copies.UpdateAsync(copy);
			var recorded = await recorder.RecordWithResultAsync(before, copy, LibraryCopyEventType.CustodyReceived,
				LibraryOperationActor.User(actor.User), input.Notes ?? "Received the physical copy from classroom custody.");
			events.Add(line.CopyId, recorded.Id);
		}
		await receipts.InsertAsync(receipt);
		foreach (var line in input.Copies) plan.Assignments[line.CopyId].Receive(receipt.Id, events[line.CopyId], line.Condition);
		await items.UpdateManyAsync(plan.Assignments.Values);
		await units.Current!.SaveChangesAsync();
		foreach (var bookId in input.Copies.Where(line => line.Condition == BookCopyStatus.Available)
			.Select(line => plan.Stock[line.CopyId].Copy.BookId).Distinct())
			await reservations.RebalanceAsync(bookId, input.ReceivedOn);
		return await ReadReceiptAsync(actor.Tenant, receipt);
	}

	private async Task<ReturnPlan> BuildReturnAsync(Guid tenant, LibraryCustodyHandoff handoff, LibraryCustodyReturnInput input, string fingerprint)
	{
		var errors = new List<LibraryCustodyErrorDto>();
		if (input.ReceivedOn < handoff.HandedOffOn)
			errors.Add(new(null, "ReturnBeforeHandoff", "The received date must be on or after the handoff."));
		var ids = input.Copies.Select(line => line.CopyId).ToArray();
		var assigned = (await items.GetListAsync(row => row.TenantId == tenant && row.HandoffId == handoff.Id &&
			Enumerable.Contains(ids, row.CopyId))).ToDictionary(row => row.CopyId);
		var stock = await inventory.LoadAsync(tenant, ids);
		var unavailable = await inventory.FindUnavailableAsync(tenant, stock, handingOff: false);
		foreach (var id in ids)
		{
			if (!assigned.TryGetValue(id, out var item) || item.ReturnReceiptId.HasValue)
				errors.Add(new(id, "NoOpenCustody", "Choose an outstanding copy from this handoff."));
			else if (!stock.TryGetValue(id, out var selected) || selected.Copy.Status != BookCopyStatus.InCustody || unavailable.Contains(id) ||
				selected.Copy.SectionId != item.HomeSectionId || selected.Copy.ShelfId != item.HomeShelfId ||
				selected.Copy.BookId != item.BookId || selected.Copy.AccessionNumber != item.AccessionNumber)
				errors.Add(new(id, "RegisterConflict", "Review this copy’s custody status, home shelf or conflicting inventory evidence."));
		}
		var previewCopies = assigned.Values.OrderBy(row => row.CopyId).Select(row => new LibraryCustodyCopyOptionDto(
			row.CopyId, row.BookId, row.Title, row.AccessionNumber,
			stock.TryGetValue(row.CopyId, out var label) ? label.Copy.Status : BookCopyStatus.InCustody)).ToList();
		var revision = errors.Count == 0 ? Hash(new { Payload = fingerprint, handoff.Id,
			Assignments = assigned.Values.OrderBy(row => row.Id).Select(row => new { row.Id, row.HandoffEventId }), Copies = StockEvidence(stock) }) : null;
		return new(new(revision, previewCopies, errors), assigned, stock);
	}

	private static LibraryCustodyReturnInput NormalizeReturn(LibraryCustodyReturnInput input)
	{
		var notes = input?.Notes?.Trim();
		if (input == null || input.ReceivedOn == default || notes?.Length > 500 || notes?.Contains('\0') == true ||
			input.Copies == null || input.Copies.Length is < 1 or > LibraryCustodyHandoff.MaximumCopies ||
			input.Copies.Any(row => row == null || !LibraryCustodyItem.IsReturnCondition(row.Condition)) ||
			!CanReadCopies(input.Copies.Select(row => row.CopyId).ToArray())) throw Invalid();
		return input with { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes, Copies = input.Copies.OrderBy(row => row.CopyId).ToArray() };
	}

	private static object StockEvidence(Dictionary<Guid, LibraryCustodyStock> stock) => stock.Values.OrderBy(row => row.Copy.Id)
		.Select(row => new { row.Copy.Id, row.Copy.BookId, row.Title, row.Copy.Status, row.Copy.AccessionNumber,
			row.Copy.SectionId, row.Copy.ShelfId, row.Copy.Remarks, row.EventSequence }).ToArray();

	private sealed record ReturnPlan(LibraryCustodyPreviewDto Preview, Dictionary<Guid, LibraryCustodyItem> Assignments,
		Dictionary<Guid, LibraryCustodyStock> Stock);
}
