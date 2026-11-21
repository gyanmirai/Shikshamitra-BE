using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using ReceivingActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Receiving;

namespace ShikshaMitra.Library.Receiving;

[RemoteService(false)]
[Authorize]
[Authorize(ReceivingActions.View)]
public partial class LibraryStockImportAppService(IPermissionChecker permissions, ILibraryAccessService access,
	LibraryStockImportLocations locations, LibraryStockImportPlanner planner, ILibraryInventoryLock inventoryLock,
	IBookAppService catalogue, ILibraryReceivingAppService receiving, IRepository<LibraryStockImportBatch, Guid> batches,
	IRepository<LibraryStockImportRow, Guid> rows, IUnitOfWorkManager units) : ApplicationService, ILibraryStockImportAppService
{
	[Authorize(ReceivingActions.Add)]
	public async Task<LibraryStockImportOptionsDto> GetOptionsAsync(Guid academicYearId)
	{
		var actor = await RequireAccessAsync(true, academicYearId);
		return await locations.ReadAsync(actor.Tenant, academicYearId, actor.Legacy, actor.CanCreate, actor.Years);
	}

	[Authorize(ReceivingActions.Add)]
	public async Task<LibraryStockImportPreviewDto> PreviewAsync(LibraryStockImportInput input)
	{
		ValidateInput(input);
		var actor = await RequireAccessAsync(true, input.AcademicYearId);
		return (await planner.BuildAsync(actor.Tenant, input, actor.Legacy, actor.CanCreate, LibraryStockImportPlanner.Fingerprint(input))).Preview;
	}

	[Authorize(ReceivingActions.Add)]
	[UnitOfWork(true)]
	public async Task<LibraryStockImportBatchDto> CommitAsync(LibraryCommitStockImportCommand command)
	{
		if (command == null || command.Revision?.Length != 64 || !command.Revision.All(Uri.IsHexDigit) ||
			string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 100 ||
			command.IdempotencyKey.Trim() != command.IdempotencyKey || command.IdempotencyKey.Any(char.IsControl))
			throw Invalid();
		var input = command.Import;
		ValidateInput(input);
		var actor = await RequireAccessAsync(true, input.AcademicYearId);
		var fingerprint = LibraryStockImportPlanner.Fingerprint(input);
		await inventoryLock.AcquireAsync(actor.Tenant);
		var previous = await batches.FindAsync(row => row.TenantId == actor.Tenant &&
			row.ImportedBy == actor.User && row.IdempotencyKey == command.IdempotencyKey);
		if (previous != null)
		{
			if (!actor.Years.Contains(previous.AcademicYearId) || previous.NewTitleCount > 0 && !actor.CanCreate)
				throw new AbpAuthorizationException();
			if (!string.Equals(previous.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
				throw new BusinessException(LibraryErrorCodes.StockImportCommandConflict);
			return await ReadAsync(actor, previous.Id);
		}
		var plan = await planner.BuildAsync(actor.Tenant, input, actor.Legacy, actor.CanCreate, fingerprint);
		if (plan.Preview.Revision != command.Revision)
			throw new BusinessException(LibraryErrorCodes.StockImportPlanChanged).WithData("Errors", plan.Preview.Errors);

		var id = GuidGenerator.Create();
		var titles = await CreateTitlesAsync(input, plan);
		var results = await ReceiveRowsAsync(id, actor.Tenant, input, plan, titles);
		var now = Clock.Now.ToUniversalTime();
		var recorded = now.AddTicks(-(now.Ticks % 10));
		await batches.InsertAsync(new(id, actor.Tenant, input.AcademicYearId, input.SectionId, input.ShelfId, actor.User,
			recorded, command.IdempotencyKey, fingerprint, results.Count, titles.Count, input.Source!.Value,
			LibraryStockImportPlanner.CleanSource(input.SourceName)));
		await rows.InsertManyAsync(results);
		await units.Current!.SaveChangesAsync();
		return await ReadAsync(actor, id);
	}

	private async Task<ImportActor> RequireAccessAsync(bool write, Guid? year = null)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid actor || actor == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(ReceivingActions.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			write && !await permissions.IsGrantedAsync(ReceivingActions.Add))
			throw new AbpAuthorizationException();
		LibraryAcademicContext context;
		try { context = await access.GetContextAsync(year); }
		catch (BusinessException exception) when (exception.Code == UserScopeErrorCodes.AccessDenied)
		{
			throw new AbpAuthorizationException();
		}
		var canCreate = write && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add);
		return new(tenant, actor, context.UserScope.HasTenantWideScope(), canCreate, context.AllowedAcademicYearIds.ToArray());
	}

	private static void ValidateInput(LibraryStockImportInput input)
	{
		if (input?.Csv == null || input.Csv.Length > LibraryStockImportCsv.MaximumBytes ||
			input.SourceName?.Length > 1024 || input.ColumnMap?.Count > 64 ||
			input.ColumnMap?.Any(pair => pair.Key.Length > 64 || pair.Value?.Length > 256) == true)
			throw Invalid();
	}

	private static BusinessException Invalid() => new(LibraryErrorCodes.StockImportInvalid,
		"Choose a valid CSV batch up to 1 MiB. Review it before submitting a new import.");

	private sealed record ImportActor(Guid Tenant, Guid User, bool Legacy, bool CanCreate, Guid[] Years);
}
