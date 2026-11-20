using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;

namespace ShikshaMitra.Library.Custody;

[RemoteService(false)]
[Authorize]
[Authorize(Actions.View)]
public partial class LibraryCustodyAppService(ILibraryAccessService access, IPermissionChecker permissions,
	LibraryCustodyReferences references, LibraryCustodyInventory inventory, ILibraryInventoryLock inventoryLock,
	LibraryCopyEventRecorder recorder, LibraryReservationCoordinator reservations, IRepository<BookCopy, Guid> copies,
	IRepository<LibraryCustodyHandoff, Guid> handoffs, IRepository<LibraryCustodyItem, Guid> items,
	IRepository<LibraryCustodyReceipt, Guid> receipts, IUnitOfWorkManager units) : ApplicationService, ILibraryCustodyAppService
{
	private async Task<CustodyActor> RequireActorAsync(string action)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid actor || actor == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(Actions.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			action != Actions.View && !await permissions.IsGrantedAsync(action)) throw new AbpAuthorizationException();
		LibraryAcademicContext context;
		try { context = await access.GetContextAsync(); }
		catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied)
		{
			throw new AbpAuthorizationException();
		}
		if (!context.UserScope.HasTenantWideScope()) throw new AbpAuthorizationException();
		return new(tenant, actor, context.AllowedAcademicYearIds.ToArray());
	}

	private static void RequireYear(CustodyActor actor, Guid year)
	{
		if (!actor.Years.Contains(year)) throw new AbpAuthorizationException();
	}

	private static void ValidateCommand(string? revision, string? key)
	{
		if (revision?.Length != 64 || !revision.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(key) ||
			key.Length > 100 || key.Trim() != key || key.Any(char.IsControl)) throw Invalid();
	}

	private static void RequireFingerprint(string accepted, string submitted)
	{
		if (!string.Equals(accepted, submitted, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.CustodyCommandConflict);
	}

	private static void RequirePreview(LibraryCustodyPreviewDto preview, string revision)
	{
		if (preview.Revision != revision)
			throw new BusinessException(LibraryErrorCodes.CustodyPlanChanged).WithData("Errors", preview.Errors);
	}

	private static bool CanReadCopies(Guid[]? ids) => ids != null && ids.Length is >= 1 and <= LibraryCustodyHandoff.MaximumCopies &&
		ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length;

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
	private static BusinessException Invalid() => new(LibraryErrorCodes.CustodyInvalid, "Review the details and physical copies before recording custody.");
	private DateTime RecordedAt()
	{
		var now = Clock.Now.ToUniversalTime();
		return now.AddTicks(-(now.Ticks % 10));
	}

	private sealed record CustodyActor(Guid Tenant, Guid User, Guid[] Years);
}
