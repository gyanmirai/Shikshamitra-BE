using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

public partial class LibraryStockVerificationAppService
{
	[Authorize(StockActions.Update)]
	[UnitOfWork(true)]
	public async Task<LibraryStockDecisionDto> SaveDecisionAsync(Guid sessionId, LibrarySaveStockDecisionCommand input)
	{
		var scope = await RequireScopeAsync(StockActions.Update);
		var reason = input?.Reason?.Trim();
		if (input == null || !Enum.IsDefined(input.Source) || !Enum.IsDefined(input.Outcome) ||
			input.RowId == Guid.Empty || input.ExpectedDecisionId == Guid.Empty ||
			!LibraryStockVerificationCommand.IsFingerprint(input.EvidenceRevision) ||
			string.IsNullOrWhiteSpace(reason) || reason.Length > LibraryStockVerificationDecision.MaximumReasonLength)
			throw Invalid();
		LibraryStockVerificationCommand.ValidateKey(input.IdempotencyKey);
		var fingerprint = Fingerprint(new { sessionId, Command = input with { Reason = reason } });
		const LibraryStockVerificationCommandKind kind = LibraryStockVerificationCommandKind.Decide;
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		var accepted = await FindAcceptedAsync(scope, input.IdempotencyKey, kind, fingerprint);
		if (accepted != null)
			return MapDecision(await decisions.GetAsync(row => row.TenantId == scope.TenantId && row.SessionId == sessionId && row.CommandId == accepted.Id));
		if (session.Status != LibraryStockVerificationStatus.InReview)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);

		var item = (await ReadReconciliationRowsAsync(ReviewScope(scope, session), session, input.Source, 1, 1, input.RowId)).Items.SingleOrDefault();
		var query = await DecisionQueryAsync(scope.TenantId, sessionId);
		var previous = await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(row => row.Source == input.Source && row.RowId == input.RowId).OrderByDescending(row => row.Version));
		if (item?.RequiresReview != true || item.EvidenceRevision != input.EvidenceRevision || previous?.Id != input.ExpectedDecisionId)
			throw new BusinessException(LibraryErrorCodes.StockVerificationPlanChanged);

		var commandId = GuidGenerator.Create();
		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var decision = new LibraryStockVerificationDecision(GuidGenerator.Create(), session, commandId, input.Source, item.Id,
			checked((previous?.Version ?? 0) + 1), item.AccessionNumber, item.Finding, item.EvidenceRevision, input.Outcome, reason,
			scope.ActorId, now);
		await commandLog.InsertAsync(new(commandId, scope.TenantId, sessionId, null, scope.ActorId, kind, input.IdempotencyKey, fingerprint, now));
		await decisions.InsertAsync(decision);
		return MapDecision(decision);
	}

	public async Task<PaginatedResponse<LibraryStockDecisionDto>> GetDecisionsAsync(Guid sessionId, int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		await RequireSessionAsync(scope, sessionId);
		var query = await DecisionQueryAsync(scope.TenantId, sessionId);
		return await PageAsync(query.OrderBy(row => row.Source).ThenBy(row => row.AccessionNumber).ThenBy(row => row.RowId).ThenByDescending(row => row.Version)
			.Select(row => new LibraryStockDecisionDto(row.Id, row.Source, row.RowId, row.Version, row.AccessionNumber,
				row.Finding, row.Outcome, row.Reason, row.DecidedBy, row.DecidedAt)), page, pageSize);
	}

	private async Task AttachDecisionsAsync(Guid tenantId, Guid sessionId, LibraryStockReconciliationSource source, List<LibraryStockReconciliationRowDto> items)
	{
		var ids = items.Select(row => row.Id).ToArray();
		var query = (await DecisionQueryAsync(tenantId, sessionId)).Where(row => row.Source == source && Enumerable.Contains(ids, row.RowId));
		var latest = (await AsyncExecuter.ToListAsync(query.Where(row => !query.Any(other => other.RowId == row.RowId && other.Version > row.Version))))
			.ToDictionary(row => row.RowId);
		for (var index = 0; index < items.Count; index++)
		{
			var item = items[index];
			if (latest.TryGetValue(item.Id, out var decision))
				items[index] = item with { Decision = MapDecision(decision), DecisionIsCurrent = item.EvidenceRevision == decision.EvidenceRevision };
		}
	}

	private async Task<IQueryable<LibraryStockVerificationDecision>> DecisionQueryAsync(Guid tenantId, Guid sessionId) =>
		(await decisions.GetQueryableAsync()).Where(row => row.TenantId == tenantId && row.SessionId == sessionId);

	private static LibraryStockDecisionDto MapDecision(LibraryStockVerificationDecision row) =>
		new(row.Id, row.Source, row.RowId, row.Version, row.AccessionNumber, row.Finding, row.Outcome, row.Reason, row.DecidedBy, row.DecidedAt);
}
