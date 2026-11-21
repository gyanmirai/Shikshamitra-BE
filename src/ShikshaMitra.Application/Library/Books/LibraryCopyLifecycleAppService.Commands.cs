using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Books;

public partial class LibraryCopyLifecycleAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryCopyStateDto> ChangeConditionAsync(Guid bookId, Guid copyId, LibraryCopyConditionRequest input)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var context = await LoadAsync(bookId, copyId);
		EnsureLoanScope(context);
		await EnsureRevisionAsync(context, input.Revision);
		var target = input.Status ?? throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition);
		LibraryCopyConditionRules.EnsureConditionChange(context.Copy.Status, target, context.Loan != null);
		var reason = LibraryCopyConditionRules.RequireReason(input.Reason);
		var before = LibraryCopySnapshot.Capture(context.Copy);
		context.Copy.Status = target;
		await SaveAsync(context, before, before.Status == BookCopyStatus.Lost ? LibraryCopyEventType.Recovered : LibraryCopyEventType.ConditionChanged, reason);
		return await StateAsync(context);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryCopyStateDto> MoveAsync(Guid bookId, Guid copyId, LibraryCopyMoveRequest input)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var context = await LoadAsync(bookId, copyId);
		EnsureLoanScope(context);
		await EnsureRevisionAsync(context, input.Revision);
		LibraryCopyConditionRules.EnsureMovable(context.Copy.Status, context.Loan != null);
		var reason = LibraryCopyConditionRules.RequireReason(input.Reason);
		var section = await sections.FindAsync(row => row.TenantId == CurrentTenant.Id && row.Id == input.SectionId)
			?? throw new BusinessException(LibraryErrorCodes.SectionNotFound);
		if (context.Copy.SectionId != section.Id)
		{
			var source = await sections.FindAsync(row => row.TenantId == CurrentTenant.Id && row.Id == context.Copy.SectionId);
			if (source?.CampusId != section.CampusId) throw new BusinessException(LibraryErrorCodes.CampusTransferRequired);
		}
		var shelf = await shelves.FindAsync(row => row.Id == input.ShelfId && row.SectionId == section.Id)
			?? throw new BusinessException(LibraryErrorCodes.ShelfNotFound, "The selected shelf was not found in this section.");
		if (context.Copy.SectionId == section.Id && context.Copy.ShelfId == shelf.Id) return await StateAsync(context);
		var before = LibraryCopySnapshot.Capture(context.Copy);
		context.Copy.SectionId = section.Id;
		context.Copy.ShelfId = shelf.Id;
		await SaveAsync(context, before, LibraryCopyEventType.Moved, reason);
		return await StateAsync(context);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)]
	public async Task<LibraryCopyStateDto> WriteOffAsync(Guid bookId, Guid copyId, LibraryCopyWriteOffRequest input)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var context = await LoadAsync(bookId, copyId);
		EnsureLoanScope(context);
		EnsureWriteOffScope(context);
		await EnsureRevisionAsync(context, input.Revision);
		LibraryCopyConditionRules.EnsureCanWriteOff(context.Copy.Status, context.Loan != null);
		var reason = LibraryCopyConditionRules.RequireReason(input.Reason);
		var before = LibraryCopySnapshot.Capture(context.Copy);
		context.Copy.Status = BookCopyStatus.Discarded;
		await SaveAsync(context, before, LibraryCopyEventType.WrittenOff, reason);
		return await StateAsync(context);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryCopyStateDto> UpdateLegacyStatusAsync(Guid bookId, string accessionNumber, UpdateBookCopyStatusRequest input)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await books.GetAsync(bookId);
		var accession = accessionNumber.Trim().ToUpperInvariant();
		var copy = await copies.FindAsync(row => row.BookId == bookId && row.NormalizedAccessionNumber == accession)
			?? throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		var context = await LoadAsync(bookId, copy.Id);
		EnsureLoanScope(context);
		if (!Enum.IsDefined(input.Status) || input.Remarks?.Length > 500)
			throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition, "Use a valid copy status and remarks of at most 500 characters.");
		var before = LibraryCopySnapshot.Capture(context.Copy);
		LibraryCopyEventType type;
		string reason;
		if (input.Status == before.Status)
		{
			if (input.Remarks == before.Remarks)
			{
				await EnsureRevisionAsync(context, input.Revision, required: false);
				return await StateAsync(context);
			}
			type = LibraryCopyEventType.NotesChanged;
			reason = string.IsNullOrWhiteSpace(input.Reason) ? "Updated copy remarks through the catalogue." : LibraryCopyConditionRules.RequireReason(input.Reason);
		}
		else if (input.Status == BookCopyStatus.Discarded)
		{
			await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete);
			EnsureWriteOffScope(context);
			LibraryCopyConditionRules.EnsureCanWriteOff(before.Status, context.Loan != null);
			type = LibraryCopyEventType.WrittenOff;
			reason = LibraryCopyConditionRules.RequireReason(input.Reason ?? input.Remarks);
		}
		else
		{
			LibraryCopyConditionRules.EnsureConditionChange(before.Status, input.Status, context.Loan != null);
			type = before.Status == BookCopyStatus.Lost ? LibraryCopyEventType.Recovered : LibraryCopyEventType.ConditionChanged;
			reason = LibraryCopyConditionRules.RequireReason(input.Reason ?? input.Remarks);
		}
		await EnsureRevisionAsync(context, input.Revision);
		context.Copy.Status = input.Status;
		context.Copy.Remarks = input.Remarks;
		await SaveAsync(context, before, type, reason);
		return await StateAsync(context);
	}

	public Task<bool> HasEventsAsync(IReadOnlyCollection<Guid> copyIds) => recorder.HasEventsAsync(copyIds);

	private static void EnsureWriteOffScope(CopyContext context)
	{
		if (!context.Academic.UserScope.HasTenantWideScope()) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task SaveAsync(CopyContext context, LibraryCopySnapshot before, LibraryCopyEventType type, string reason)
	{
		var actorId = CurrentUser.Id ?? throw new AbpAuthorizationException();
		await recorder.RecordAsync(before, context.Copy, type, actorId, reason, context.Loan?.Id);
		await copies.UpdateAsync(context.Copy, autoSave: true);
		if (context.Copy.Status == BookCopyStatus.Available) await reservationCoordinator.RebalanceAsync(context.Copy.BookId);
	}
}
