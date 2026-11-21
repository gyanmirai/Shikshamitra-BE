using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Authorization;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

public partial class LibraryStockVerificationAppService
{
	public async Task<LibraryStockVerificationOptionsDto> GetOptionsAsync()
	{
		var scope = await RequireScopeAsync(StockActions.View);
		var query = await SectionQueryAsync(scope);
		var allowed = await AsyncExecuter.ToListAsync(query.OrderBy(row => row.SectionName).ThenBy(row => row.Id)
			.Select(row => new { row.Id, row.SectionName }));
		var ids = allowed.Select(row => row.Id).ToArray();
		var availableShelves = (await shelves.GetQueryableAsync()).Where(row => Enumerable.Contains(ids, row.SectionId));
		var locations = (await AsyncExecuter.ToListAsync(availableShelves.OrderBy(row => row.ShelfName).ThenBy(row => row.Id)
			.Select(row => new { row.SectionId, Shelf = new LibraryCopyMoveShelfDto { Id = row.Id, ShelfName = row.ShelfName ?? string.Empty } })))
			.ToLookup(row => row.SectionId, row => row.Shelf);
		var day = await LibraryReadDateEvidence.ReadAsync(calendar, Clock);
		return new(scope.TenantWide, day.Day, day.Source, allowed.Select(row => new LibraryCopyMoveSectionDto
			{ Id = row.Id, SectionName = row.SectionName ?? string.Empty, ShelfDetails = locations[row.Id].ToList() }).ToArray());
	}

	public async Task<LibraryStockVerificationSessionDto> GetAsync(Guid sessionId) =>
		Map(await RequireSessionAsync(await RequireScopeAsync(StockActions.View), sessionId));

	public async Task<PaginatedResponse<LibraryStockVerificationSessionDto>> GetListAsync(int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		var query = (await SessionQueryAsync(scope)).OrderByDescending(row => row.SchoolDate).ThenByDescending(row => row.Id);
		return await PageAsync(query.Select(row => new LibraryStockVerificationSessionDto(row.Id, row.Name, row.SectionId,
			row.ShelfId, row.AcademicYearId, row.SchoolDate, row.ExpectedCopyCount, row.ObservationCount, row.Status, row.OpenedBy,
			row.OpenedAt, row.ReviewStartedBy, row.ReviewStartedAt, row.FinalizedBy, row.FinalizedAt, row.CancelledBy, row.CancelledAt,
			row.CancellationReason)), page, pageSize);
	}

	public async Task<PaginatedResponse<LibraryStockBaselineDto>> GetBaselineAsync(Guid sessionId, int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		await RequireSessionAsync(scope, sessionId);
		var query = (await baseline.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == sessionId)
			.OrderBy(row => row.AccessionNumber).ThenBy(row => row.CopyId);
		return await PageAsync(query.Select(row => new LibraryStockBaselineDto(row.CopyId, row.BookId, row.AccessionNumber,
			row.SectionId, row.ShelfId, row.CopyStatus)), page, pageSize);
	}

	public async Task<PaginatedResponse<LibraryStockObservationDto>> GetObservationsAsync(Guid sessionId, int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		await RequireSessionAsync(scope, sessionId);
		var query = (await observations.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == sessionId)
			.OrderByDescending(row => row.Sequence);
		return await PageAsync(query.Select(row => new LibraryStockObservationDto(row.Id, row.Sequence, row.AccessionNumber, row.CopyId,
			row.ObservedSectionId, row.ObservedShelfId, row.SchoolDate, row.ObservedBy, row.ObservedAt)), page, pageSize);
	}

	private async Task<StockScope> RequireScopeAsync(string action)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is not Guid actor || actor == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(StockActions.View) || !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			(action != StockActions.View && !await permissions.IsGrantedAsync(action))) throw new AbpAuthorizationException();
		var academic = await access.GetContextAsync();
		return new(tenant, actor, academic.UserScope.HasTenantWideScope(), academic.AllowedAcademicYearIds.ToArray());
	}

	private async Task<IQueryable<LibrarySection>> SectionQueryAsync(StockScope scope)
	{
		var query = (await sections.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId);
		return scope.TenantWide ? query : query.Where(row => row.AcademicYearId.HasValue && Enumerable.Contains(scope.Years, row.AcademicYearId.Value));
	}

	private async Task<Guid?> ResolveLocationAsync(StockScope scope, Guid? section, Guid? shelf, bool requiredCountScope)
	{
		if (section == Guid.Empty || shelf == Guid.Empty || shelf.HasValue && !section.HasValue) throw Invalid();
		if (!section.HasValue)
		{
			if (requiredCountScope && !scope.TenantWide) throw new AbpAuthorizationException();
			return null;
		}
		var row = await AsyncExecuter.FirstOrDefaultAsync((await SectionQueryAsync(scope)).Where(candidate => candidate.Id == section.Value));
		if (row == null) throw new AbpAuthorizationException();
		if (shelf.HasValue && !await AsyncExecuter.AnyAsync((await shelves.GetQueryableAsync())
			.Where(candidate => candidate.Id == shelf.Value && candidate.SectionId == row.Id))) throw Invalid();
		return row.AcademicYearId;
	}

	private async Task<IQueryable<LibraryStockVerificationSession>> SessionQueryAsync(StockScope scope)
	{
		var query = (await sessions.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId);
		return scope.TenantWide ? query : query.Where(row => row.SectionId.HasValue && row.AcademicYearId.HasValue &&
			Enumerable.Contains(scope.Years, row.AcademicYearId.Value));
	}

	private async Task<LibraryStockVerificationSession> RequireSessionAsync(StockScope scope, Guid id) =>
		await AsyncExecuter.FirstOrDefaultAsync((await SessionQueryAsync(scope)).Where(row => row.Id == id)) ?? throw new AbpAuthorizationException();

	private async Task<PaginatedResponse<T>> PageAsync<T>(IQueryable<T> query, int page, int pageSize, int maximumPageSize = 100)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, maximumPageSize);
		var count = await AsyncExecuter.CountAsync(query);
		var offset = (long)(page - 1) * pageSize;
		return new() { CurrentPage = page, PageSize = pageSize, TotalCount = count, TotalPages = (int)Math.Ceiling((double)count / pageSize),
			Items = offset >= count ? [] : await AsyncExecuter.ToListAsync(query.Skip((int)offset).Take(pageSize)) };
	}

	private static LibraryStockVerificationSessionDto Map(LibraryStockVerificationSession row) => new(row.Id, row.Name, row.SectionId,
		row.ShelfId, row.AcademicYearId, row.SchoolDate, row.ExpectedCopyCount, row.ObservationCount, row.Status, row.OpenedBy,
		row.OpenedAt, row.ReviewStartedBy, row.ReviewStartedAt, row.FinalizedBy, row.FinalizedAt, row.CancelledBy, row.CancelledAt, row.CancellationReason);

	private static LibraryStockObservationDto Map(LibraryStockVerificationObservation row) => new(row.Id, row.Sequence,
		row.AccessionNumber, row.CopyId, row.ObservedSectionId, row.ObservedShelfId, row.SchoolDate, row.ObservedBy, row.ObservedAt);

	private sealed record StockScope(Guid TenantId, Guid ActorId, bool TenantWide, Guid[] Years);
}
