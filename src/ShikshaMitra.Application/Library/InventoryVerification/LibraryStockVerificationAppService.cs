using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.CampusTransfers;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

[RemoteService(false)]
[Authorize]
[Authorize(StockActions.View)]
public partial class LibraryStockVerificationAppService(IPermissionChecker permissions, ILibraryAccessService access,
	LibraryCalendarService calendar, IUnitOfWorkManager units, ILibraryInventoryLock inventoryLock,
	IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves, IRepository<BookCopy, Guid> copies,
	IRepository<LibraryStockVerificationSession, Guid> sessions, IRepository<LibraryStockVerificationBaseline, Guid> baseline,
	IRepository<LibraryStockVerificationObservation, Guid> observations, IRepository<LibraryStockVerificationCommand, Guid> commandLog,
	LibraryCopyScanReader scans, IRepository<BookIssue, Guid> loans, IRepository<LibraryHold, Guid> holds,
	IRepository<LibraryStockVerificationDecision, Guid> decisions, IRepository<LibraryStockVerificationResult, Guid> publishedRows,
	LibraryStockReceiptCutoffReader receiptCutoff, IRepository<LibraryCustodyItem, Guid> custody,
	IRepository<LibraryCampusTransferItem, Guid> transferItems, IRepository<LibraryCampusTransfer, Guid> transfers)
	: ApplicationService, ILibraryStockVerificationAppService
{
	[Authorize(StockActions.Add)]
	public async Task<LibraryStockCountPreviewDto> PreviewAsync(LibraryStockCountInput input)
	{
		var scope = await RequireScopeAsync(StockActions.Add);
		var (preview, _, _) = await PreviewCoreAsync(scope, Normalize(input));
		return preview;
	}

	[Authorize(StockActions.Add)]
	[UnitOfWork(true)]
	public async Task<LibraryStockVerificationSessionDto> OpenAsync(LibraryOpenStockCountCommand input)
	{
		var scope = await RequireScopeAsync(StockActions.Add);
		if (input == null) throw Invalid();
		var count = Normalize(input.Count);
		LibraryStockVerificationCommand.ValidateKey(input.IdempotencyKey);
		if (!LibraryStockVerificationCommand.IsFingerprint(input.Revision)) throw Invalid();
		var fingerprint = Fingerprint(new { Count = count, input.Revision });
		await inventoryLock.AcquireAsync(scope.TenantId);
		var accepted = await FindAcceptedAsync(scope, input.IdempotencyKey, LibraryStockVerificationCommandKind.Open, fingerprint);
		if (accepted != null) return Map(await RequireSessionAsync(scope, accepted.SessionId));

		var (preview, inventory, year) = await PreviewCoreAsync(scope, count);
		if (preview.Revision != input.Revision) throw new BusinessException(LibraryErrorCodes.StockVerificationPlanChanged);
		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var session = new LibraryStockVerificationSession(GuidGenerator.Create(), scope.TenantId, count.Name, count.SectionId,
			count.ShelfId, preview.SchoolDate, preview.ExpectedCopyCount, scope.ActorId, now, year);
		await sessions.InsertAsync(session);
		foreach (var batch in inventory.Chunk(500))
			await baseline.InsertManyAsync(batch.Select(copy => new LibraryStockVerificationBaseline(GuidGenerator.Create(), session, copy)));
		await commandLog.InsertAsync(new(GuidGenerator.Create(), scope.TenantId, session.Id, null, scope.ActorId,
			LibraryStockVerificationCommandKind.Open, input.IdempotencyKey, fingerprint, now));
		await units.Current!.SaveChangesAsync();
		return Map(session);
	}

	[Authorize(StockActions.Update)]
	[UnitOfWork(true)]
	public async Task<LibraryStockObservationDto> RecordObservationAsync(Guid sessionId, LibraryRecordStockObservationCommand input)
	{
		var scope = await RequireScopeAsync(StockActions.Update);
		if (input == null) throw Invalid();
		LibraryStockVerificationCommand.ValidateKey(input.IdempotencyKey);
		var accession = input.Scan?.Trim();
		if (string.IsNullOrWhiteSpace(accession) || accession.Length > BookCopy.MaximumAccessionLength || accession.Any(char.IsControl))
			throw Invalid();
		var fingerprint = Fingerprint(new { sessionId, Scan = accession, input.SectionId, input.ShelfId, input.SchoolDate });
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		var accepted = await FindAcceptedAsync(scope, input.IdempotencyKey, LibraryStockVerificationCommandKind.Observe, fingerprint);
		if (accepted != null)
			return Map(await observations.GetAsync(row => row.TenantId == scope.TenantId && row.SessionId == sessionId && row.Id == accepted.ObservationId));

		await ResolveLocationAsync(scope, input.SectionId, input.ShelfId, requiredCountScope: false);
		var copy = await scans.FindAsync(scope.TenantId, accession, scope.TenantWide ? null : (await SectionQueryAsync(scope)).Select(row => row.Id));
		var day = await RequireSchoolDateAsync(input.SchoolDate);
		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var observation = new LibraryStockVerificationObservation(GuidGenerator.Create(), session, accession,
			copy == null ? null : LibraryCopySnapshot.Capture(copy), input.SectionId, input.ShelfId, day.Day!.Value, scope.ActorId, now);
		await sessions.UpdateAsync(session);
		await observations.InsertAsync(observation);
		await commandLog.InsertAsync(new(GuidGenerator.Create(), scope.TenantId, sessionId, observation.Id, scope.ActorId,
			LibraryStockVerificationCommandKind.Observe, input.IdempotencyKey, fingerprint, now));
		await units.Current!.SaveChangesAsync();
		return Map(observation);
	}

	private async Task<(LibraryStockCountPreviewDto Preview, LibraryCopySnapshot[] Copies, Guid? Year)> PreviewCoreAsync(StockScope scope, LibraryStockCountInput input)
	{
		var year = await ResolveLocationAsync(scope, input.SectionId, input.ShelfId, requiredCountScope: true);
		var day = await RequireSchoolDateAsync(input.SchoolDate);
		var query = (await copies.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.Status != BookCopyStatus.Discarded);
		if (input.SectionId.HasValue) query = query.Where(row => row.SectionId == input.SectionId.Value);
		if (input.ShelfId.HasValue) query = query.Where(row => row.ShelfId == input.ShelfId.Value);
		var inventory = (await AsyncExecuter.ToListAsync(query.OrderBy(row => row.Id).Select(row => new LibraryCopySnapshot(
			row.Id, row.BookId, row.TenantId, row.AccessionNumber, row.Status, row.SectionId, row.ShelfId, null)))).ToArray();
		var revision = Fingerprint(new { Count = input, Day = day.Day!.Value, Year = year, Copies = inventory });
		var totals = inventory.GroupBy(row => row.Status).OrderBy(group => group.Key)
			.Select(group => new LibraryStockCountStatusTotal(group.Key, group.Count())).ToArray();
		return (new(day.Day.Value, day.Source, inventory.Length, totals, revision), inventory, year);
	}

	private async Task<LibraryReadSchoolDay> RequireSchoolDateAsync(DateOnly? requested)
	{
		if (requested == DateOnly.MinValue) throw Invalid();
		var day = await LibraryReadDateEvidence.ReadAsync(calendar, Clock, requested);
		if (!day.Day.HasValue) throw new BusinessException(LibraryErrorCodes.CalendarNotConfigured);
		return day;
	}

	private async Task<LibraryStockVerificationCommand?> FindAcceptedAsync(StockScope scope, string key,
		LibraryStockVerificationCommandKind kind, string fingerprint)
	{
		var accepted = await commandLog.FindAsync(row => row.TenantId == scope.TenantId && row.ActorId == scope.ActorId && row.IdempotencyKey == key);
		if (accepted != null && (accepted.Kind != kind || accepted.PayloadFingerprint != fingerprint))
			throw new BusinessException(LibraryErrorCodes.StockVerificationCommandConflict);
		return accepted;
	}

	private static LibraryStockCountInput Normalize(LibraryStockCountInput input)
	{
		var name = input?.Name?.Trim();
		if (input == null || string.IsNullOrWhiteSpace(name) || name.Length > LibraryStockVerificationSession.MaximumNameLength || name.Any(char.IsControl))
			throw Invalid();
		return input with { Name = name };
	}

	private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
	private static BusinessException Invalid() => new(LibraryErrorCodes.StockVerificationInvalidInput);
}
