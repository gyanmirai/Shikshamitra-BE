using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;

namespace ShikshaMitra.Library.Custody;

public partial class LibraryCustodyAppService
{
	[Authorize(Actions.Add)]
	public async Task<LibraryCustodyOptionsDto> GetOptionsAsync(Guid academicYearId)
	{
		var actor = await RequireActorAsync(Actions.Add);
		RequireYear(actor, academicYearId);
		return await references.GetOptionsAsync(actor.Tenant, academicYearId, actor.Years);
	}

	[Authorize(Actions.Add)]
	public async Task<List<LibraryCustodyCopyOptionDto>> FindCopiesAsync(string search) =>
		await inventory.SearchAsync((await RequireActorAsync(Actions.Add)).Tenant, search);

	[Authorize(Actions.Add)]
	public async Task<LibraryCustodyPreviewDto> PreviewHandoffAsync(LibraryCustodyHandoffInput input)
	{
		var actor = await RequireActorAsync(Actions.Add);
		var normalized = NormalizeHandoff(input);
		RequireYear(actor, normalized.AcademicYearId);
		return (await BuildHandoffAsync(actor.Tenant, normalized, Hash(normalized))).Preview;
	}

	[Authorize(Actions.Add)]
	[UnitOfWork(true)]
	public async Task<LibraryCustodyHandoffDto> HandoffAsync(LibraryCustodyHandoffCommand command)
	{
		var actor = await RequireActorAsync(Actions.Add);
		ValidateCommand(command?.Revision, command?.IdempotencyKey);
		var input = NormalizeHandoff(command!.Handoff);
		await inventoryLock.AcquireAsync(actor.Tenant);
		var fingerprint = Hash(input);
		var saved = await handoffs.FindAsync(row => row.TenantId == actor.Tenant && row.ActorId == actor.User && row.IdempotencyKey == command.IdempotencyKey);
		if (saved != null)
		{
			RequireFingerprint(saved.PayloadFingerprint, fingerprint);
			return Map(saved);
		}
		RequireYear(actor, input.AcademicYearId);
		var plan = await BuildHandoffAsync(actor.Tenant, input, fingerprint);
		RequirePreview(plan.Preview, command.Revision);
		var id = GuidGenerator.Create();
		var outcomes = new List<LibraryCustodyItem>(plan.Stock.Count);
		foreach (var selected in plan.Stock.Values.OrderBy(row => row.Copy.Id))
		{
			var copy = selected.Copy;
			var before = LibraryCopySnapshot.Capture(copy);
			copy.Status = BookCopyStatus.InCustody;
			await copies.UpdateAsync(copy);
			var history = await recorder.RecordWithResultAsync(before, copy, LibraryCopyEventType.CustodyHandedOff,
				LibraryOperationActor.User(actor.User), input.Purpose);
			outcomes.Add(new(GuidGenerator.Create(), actor.Tenant, id, copy, selected.Title, history.Id));
		}
		var selectedContext = plan.Reference;
		var handoff = new LibraryCustodyHandoff(id, actor.Tenant, input.AcademicYearId, input.EmployeeId, selectedContext.Custodian!,
			input.StandardId, selectedContext.ClassName, input.RoomId, selectedContext.RoomName, input.Purpose, input.HandedOffOn,
			input.ExpectedReturnOn, actor.User, RecordedAt(), outcomes.Count, command.IdempotencyKey, fingerprint);
		await handoffs.InsertAsync(handoff);
		await items.InsertManyAsync(outcomes);
		await units.Current!.SaveChangesAsync();
		return Map(handoff);
	}

	private async Task<HandoffPlan> BuildHandoffAsync(Guid tenant, LibraryCustodyHandoffInput input, string fingerprint)
	{
		var errors = new List<LibraryCustodyErrorDto>();
		var context = await references.ReadAsync(tenant, input);
		if (string.IsNullOrWhiteSpace(context.Custodian))
			errors.Add(new(null, "EmployeeUnavailable", "Choose an active school employee as custodian."));
		if (input.StandardId.HasValue && context.ClassName == null)
			errors.Add(new(null, "ClassUnavailable", "Choose a class in the selected school year."));
		if (input.RoomId.HasValue && context.RoomName == null)
			errors.Add(new(null, "RoomUnavailable", "Choose a room at this school."));
		if (input.HandedOffOn < context.YearStartsOn || input.HandedOffOn > context.YearEndsOn)
			errors.Add(new(null, "HandoffDateOutsideYear", "Record the handoff date within the selected academic year."));
		var stock = await inventory.LoadAsync(tenant, input.CopyIds);
		var unavailable = await inventory.FindUnavailableAsync(tenant, stock, handingOff: true);
		foreach (var copyId in input.CopyIds)
			if (!stock.TryGetValue(copyId, out var row) || row.Copy.Status != BookCopyStatus.Available || unavailable.Contains(copyId))
				errors.Add(new(copyId, "CopyUnavailable", "Choose a copy available at a known shelf without an open loan, hold or custody."));
		var summary = stock.Values.OrderBy(row => row.Copy.Id).Select(row => row.Label).ToList();
		var revision = errors.Count == 0 ? Hash(new { Payload = fingerprint, Context = context, Copies = StockEvidence(stock) }) : null;
		return new(new(revision, summary, errors), context, stock);
	}

	private static LibraryCustodyHandoffInput NormalizeHandoff(LibraryCustodyHandoffInput input)
	{
		var purpose = input?.Purpose?.Trim();
		if (input == null || input.EmployeeId == Guid.Empty || input.StandardId == Guid.Empty || input.RoomId == Guid.Empty ||
			string.IsNullOrWhiteSpace(purpose) || purpose.Length > 500 || purpose.Contains('\0') ||
			input.HandedOffOn == default || input.ExpectedReturnOn < input.HandedOffOn || !CanReadCopies(input.CopyIds)) throw Invalid();
		return input with { Purpose = purpose, CopyIds = input.CopyIds.Order().ToArray() };
	}

	private sealed record HandoffPlan(LibraryCustodyPreviewDto Preview, LibraryCustodyReferenceSnapshot Reference,
		Dictionary<Guid, LibraryCustodyStock> Stock);
}
