using System;
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

namespace ShikshaMitra.Library.Receiving;

[RemoteService(false)]
[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
public partial class LibraryReceivingAppService(IPermissionChecker permissions, ILibraryAccessService access,
	LibraryReceivingPreviewBuilder previewBuilder, LibraryReceivingCopyFactory stockFactory, LibraryReceivingLocationReader locations,
	LibraryReceivingLabelReader currentLabels,
	IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<LibraryStockReceipt, Guid> receipts, IRepository<LibraryStockReceiptLine, Guid> receiptLines,
	IRepository<LibraryReceivedCopy, Guid> receivedCopies, IRepository<LibraryStockReceiptCommand, Guid> commands,
	ILibraryInventoryLock inventoryLock, LibraryReservationCoordinator reservations, IUnitOfWorkManager units)
	: ApplicationService, ILibraryReceivingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public async Task<LibraryReceivingOptionsDto> GetOptionsAsync()
	{
		await EnsureAccessAsync(true);
		return new() { Sections = await locations.ReadAsync(CurrentTenant.Id!.Value) };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public async Task<LibraryReceivingPreviewDto> PreviewAsync(LibraryReceivingInput input)
	{
		await EnsureAccessAsync(true);
		return await previewBuilder.BuildAsync(CurrentTenant.Id!.Value, input);
	}

	private async Task EnsureAccessAsync(bool receive)
	{
		if (!CurrentUser.IsAuthenticated || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			receive && !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.Add))
			throw new AbpAuthorizationException();
		if (!CurrentTenant.Id.HasValue || CurrentTenant.Id == Guid.Empty || !CurrentUser.Id.HasValue ||
			CurrentUser.TenantId != CurrentTenant.Id)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		await access.GetContextAsync();
	}
}
