using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

public partial class LibraryStockVerificationAppService
{
	private const int ApprovalBatchSize = 256;
	private const LibraryStockVerificationCommandKind ApprovalKind = LibraryStockVerificationCommandKind.Finalize;

	[Authorize(StockActions.Approve)]
	[UnitOfWork(true)]
	public async Task<LibraryStockApprovalPreviewDto> GetApprovalPreviewAsync(Guid sessionId)
	{
		var scope = await RequireScopeAsync(StockActions.Approve);
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		return await ReadApprovalAsync(ReviewScope(scope, session), session, null);
	}

	[Authorize(StockActions.Approve)]
	[UnitOfWork(true)]
	public async Task<LibraryStockTransitionDto> FinalizeAsync(Guid sessionId, LibraryFinalizeStockCountCommand input)
	{
		var scope = await RequireScopeAsync(StockActions.Approve);
		if (input == null || !LibraryStockVerificationCommand.IsFingerprint(input.Revision)) throw Invalid();
		LibraryStockVerificationCommand.ValidateKey(input.IdempotencyKey);
		var fingerprint = Fingerprint(new { sessionId, input.Revision });
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		if (await FindAcceptedAsync(scope, input.IdempotencyKey, ApprovalKind, fingerprint) != null)
			return new(sessionId, LibraryStockVerificationStatus.Finalized, session.ObservationCount);

		var entries = new List<ApprovalEntry>();
		var preview = await ReadApprovalAsync(ReviewScope(scope, session), session, entries);
		if (input.Revision != preview.Revision) throw new BusinessException(LibraryErrorCodes.StockVerificationPlanChanged);
		if (!preview.CanFinalize) throw new BusinessException(LibraryErrorCodes.StockVerificationDecisionsRequired);

		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var commandId = GuidGenerator.Create();
		session.FinalizeReview(scope.ActorId, now, preview.Revision);
		await sessions.UpdateAsync(session);
		await commandLog.InsertAsync(new(commandId, scope.TenantId, sessionId, null, scope.ActorId, ApprovalKind, input.IdempotencyKey, fingerprint, now));
		foreach (var chunk in entries.Chunk(ApprovalBatchSize))
			await publishedRows.InsertManyAsync(chunk.Select(entry =>
				new LibraryStockVerificationResult(GuidGenerator.Create(), session, commandId, entry.Source, entry.Position, entry.Evidence)));
		return new(sessionId, session.Status, session.ObservationCount);
	}

	public async Task<LibraryStockPublishedResultDto> GetPublishedResultAsync(Guid sessionId,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		if (!Enum.IsDefined(source)) throw Invalid();
		var session = await RequireSessionAsync(scope, sessionId);
		if (session.Status != LibraryStockVerificationStatus.Finalized || !await AsyncExecuter.AnyAsync(
			(await commandLog.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == sessionId && row.Kind == ApprovalKind)))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);

		var query = (await publishedRows.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == sessionId);
		var totals = await AsyncExecuter.ToListAsync(query.GroupBy(row => row.DecisionId.HasValue)
			.Select(group => new { HasDecision = group.Key, Count = group.Count() }));
		var slice = await PageAsync(query.Where(row => row.Source == source).OrderBy(row => row.Position), page, pageSize);
		var ids = slice.Items.Where(row => row.DecisionId.HasValue).Select(row => row.DecisionId!.Value).ToArray();
		var recorded = ids.Length == 0 ? new Dictionary<Guid, LibraryStockDecisionDto>() :
			(await AsyncExecuter.ToListAsync((await DecisionQueryAsync(scope.TenantId, sessionId))
				.Where(row => Enumerable.Contains(ids, row.Id)))).ToDictionary(row => row.Id, MapDecision);
		var result = new PaginatedResponse<LibraryStockPublishedRowDto>
		{
			CurrentPage = slice.CurrentPage, PageSize = slice.PageSize, TotalCount = slice.TotalCount, TotalPages = slice.TotalPages,
			Items = slice.Items.ConvertAll(row => new LibraryStockPublishedRowDto(row.RowId, row.AccessionNumber, row.Finding, row.BaselineStatus,
				row.CurrentStatus, row.ExpectedLocation, row.ObservedLocation, row.ObservationSequence,
				row.DecisionId.HasValue ? recorded[row.DecisionId.Value] : null, row.ReceivedAfterOpening))
		};
		return new(session.FinalizedAt!.Value, session.FinalizedBy!.Value, session.ExpectedCopyCount, session.ObservationCount,
			totals.Sum(row => row.Count), totals.Where(row => row.HasDecision).Sum(row => row.Count), result);
	}

	private async Task<LibraryStockApprovalPreviewDto> ReadApprovalAsync(StockScope scope, LibraryStockVerificationSession session,
		List<ApprovalEntry>? entries)
	{
		if (session.Status != LibraryStockVerificationStatus.InReview)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		using var baselineTracking = baseline.DisableTracking();
		using var observationTracking = observations.DisableTracking();
		using var copyTracking = copies.DisableTracking();
		using var decisionTracking = decisions.DisableTracking();
		var locations = await ReadApprovalLocationsAsync(scope);
		using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		digest.AppendData(Convert.FromHexString(Fingerprint(new { session.Id, session.ObservationCount, session.ExpectedCopyCount })));
		int position = 0, flagged = 0, missing = 0, stale = 0;
		foreach (var source in Enum.GetValues<LibraryStockReconciliationSource>())
		{
			for (var page = 1; ; page++)
			{
				var slice = await ReadReconciliationRowsAsync(scope, session, source, page, ApprovalBatchSize, maximumPageSize: ApprovalBatchSize);
				if (slice.Items.Count == 0) break;
				await AttachDecisionsAsync(scope.TenantId, session.Id, source, slice.Items);
				foreach (var row in slice.Items)
				{
					if (row.RequiresReview)
					{
						flagged++;
						if (row.Decision == null) missing++;
						else if (!row.DecisionIsCurrent) stale++;
					}
					var evidence = new LibraryStockPublishedEvidence(row.Id, row.AccessionNumber, row.Finding, row.BaselineStatus, row.CurrentStatus,
						locations.Label(row.ExpectedSectionId, row.ExpectedShelfId), locations.Label(row.ObservedSectionId, row.ObservedShelfId),
						row.ObservationSequence, row.EvidenceRevision, row.RequiresReview && row.DecisionIsCurrent ? row.Decision?.Id : null,
						row.ReceivedAfterOpening);
					var entry = new ApprovalEntry(source, ++position, evidence);
					digest.AppendData(Convert.FromHexString(Fingerprint(new { Entry = entry, LatestDecision = row.Decision?.Id })));
					entries?.Add(entry);
				}
				if (page >= slice.TotalPages) break;
			}
		}
		return new(session.ExpectedCopyCount, session.ObservationCount, position, flagged, missing, stale, Convert.ToHexString(digest.GetHashAndReset()));
	}

	private async Task<ApprovalLocations> ReadApprovalLocationsAsync(StockScope scope)
	{
		var allowed = await SectionQueryAsync(scope);
		var sectionsById = (await AsyncExecuter.ToListAsync(allowed.Select(row => new { row.Id, row.SectionName })))
			.ToDictionary(row => row.Id, row => row.SectionName);
		var ids = allowed.Select(row => row.Id);
		var shelvesById = (await AsyncExecuter.ToListAsync((await shelves.GetQueryableAsync())
			.Where(row => ids.Contains(row.SectionId)).Select(row => new { row.SectionId, row.Id, row.ShelfName })))
			.ToDictionary(row => (row.SectionId, row.Id), row => row.ShelfName);
		return new(sectionsById, shelvesById);
	}

	private sealed record ApprovalEntry(LibraryStockReconciliationSource Source, int Position, LibraryStockPublishedEvidence Evidence);

	private sealed record ApprovalLocations(Dictionary<Guid, string?> Sections, Dictionary<(Guid, Guid), string?> Shelves)
	{
		public string? Label(Guid? sectionId, Guid? shelfId)
		{
			if (!sectionId.HasValue || !Sections.TryGetValue(sectionId.Value, out var section)) return null;
			return shelfId.HasValue && Shelves.TryGetValue((sectionId.Value, shelfId.Value), out var shelf) ? $"{section} / {shelf}" : section;
		}
	}
}
