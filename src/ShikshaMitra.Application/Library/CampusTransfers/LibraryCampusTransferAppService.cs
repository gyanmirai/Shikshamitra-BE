using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.CampusTransfers;

namespace ShikshaMitra.Library.CampusTransfers;

[RemoteService(false)]
[Authorize]
[Authorize(Actions.View)]
public partial class LibraryCampusTransferAppService(ILibraryAccessService access, IPermissionChecker permissions,
	LibraryCampusTransferLocations locations, LibraryCustodyInventory inventory, ILibraryInventoryLock inventoryLock,
	IRepository<LibraryCampusTransfer, Guid> transfers, IRepository<LibraryCampusTransferItem, Guid> items,
	IRepository<LibraryCampusTransferReceipt, Guid> receipts, IRepository<BookCopy, Guid> copies,
	LibraryCopyEventRecorder recorder, LibraryReservationCoordinator reservations, IUnitOfWorkManager units)
	: ApplicationService, ILibraryCampusTransferAppService
{
	private async Task<TransferActor> RequireActorAsync(string action)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid user || user == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(Actions.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			action != Actions.View && !await permissions.IsGrantedAsync(action)) throw new AbpAuthorizationException();
		try
		{
			var context = await access.GetContextAsync();
			if (context.UserScope.HasTenantWideScope()) return new(tenant, user);
		}
		catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied) { }
		throw new AbpAuthorizationException();
	}

	private async Task<LibraryCampusTransfer> FindTransferAsync(Guid tenant, Guid id) =>
		await transfers.FindAsync(row => row.TenantId == tenant && row.Id == id)
		?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(LibraryCampusTransfer), id);

	private DateTime RecordedAt()
	{
		var now = Clock.Now.ToUniversalTime();
		return now.AddTicks(-(now.Ticks % 10));
	}

	private static bool ValidCopies(Guid[]? ids) => ids != null && ids.Length is >= 1 and <= LibraryCampusTransfer.MaximumCopies &&
		ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length;

	private static void RequireCommand(string? revision, string? key)
	{
		if (revision?.Length != 64 || !revision.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(key) ||
			key.Length > 100 || key.Trim() != key || key.Any(char.IsControl)) throw Invalid();
	}

	private static void RequirePreview(LibraryCampusTransferPreview preview, string revision)
	{
		if (preview.Revision != revision)
			throw new BusinessException(LibraryErrorCodes.CampusTransferPlanChanged).WithData("Errors", preview.Errors);
	}

	private static void RequireExactRetry(string accepted, string submitted)
	{
		if (accepted != submitted) throw new BusinessException(LibraryErrorCodes.CampusTransferCommandConflict);
	}

	private static string Hash<T>(T data) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));
	private static BusinessException Invalid() => new(LibraryErrorCodes.CampusTransferInvalid);
	private static LibraryCampusTransferCopyOption MapStock(LibraryCustodyCopyOptionDto stock) =>
		new(stock.CopyId, stock.BookId, stock.Title, stock.AccessionNumber, stock.Status);
	private static object StockEvidence(LibraryCustodyStock row) =>
		new { row.Copy.Id, row.Copy.BookId, row.Copy.AccessionNumber, row.Copy.SectionId, row.Copy.ShelfId,
			row.Copy.Status, row.Copy.Remarks, row.Title, row.EventSequence };
	private sealed record TransferActor(Guid Tenant, Guid User);
}
