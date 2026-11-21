using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

public partial class LibraryStockVerificationAppService
{
	[Authorize(StockActions.Update)]
	[UnitOfWork(true)]
	public Task<LibraryStockTransitionDto> BeginReviewAsync(Guid sessionId, LibraryBeginStockReviewCommand input)
	{
		if (input == null) throw Invalid();
		return TransitionAsync(sessionId, LibraryStockVerificationCommandKind.BeginReview, LibraryStockVerificationStatus.Counting,
			input.ExpectedObservationCount, null, input.IdempotencyKey);
	}

	[Authorize(StockActions.Update)]
	[UnitOfWork(true)]
	public Task<LibraryStockTransitionDto> CancelAsync(Guid sessionId, LibraryCancelStockCountCommand input)
	{
		if (input == null) throw Invalid();
		return TransitionAsync(sessionId, LibraryStockVerificationCommandKind.Cancel, input.ExpectedStatus,
			input.ExpectedObservationCount, input.Reason?.Trim(), input.IdempotencyKey);
	}

	private async Task<LibraryStockTransitionDto> TransitionAsync(Guid sessionId, LibraryStockVerificationCommandKind kind,
		LibraryStockVerificationStatus? expectedStatus, int? expectedObservationCount, string? reason, string key)
	{
		var scope = await RequireScopeAsync(StockActions.Update);
		var cancelling = kind == LibraryStockVerificationCommandKind.Cancel;
		LibraryStockVerificationCommand.ValidateKey(key);
		if (expectedObservationCount is null or < 0 ||
			expectedStatus is not (LibraryStockVerificationStatus.Counting or LibraryStockVerificationStatus.InReview) ||
			cancelling && (string.IsNullOrWhiteSpace(reason) || reason.Length > LibraryStockVerificationSession.MaximumCancellationReasonLength))
			throw Invalid();

		var result = new LibraryStockTransitionDto(sessionId,
			cancelling ? LibraryStockVerificationStatus.Cancelled : LibraryStockVerificationStatus.InReview, expectedObservationCount.Value);
		var fingerprint = Fingerprint(new { sessionId, expectedStatus, expectedObservationCount, reason });
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		if (await FindAcceptedAsync(scope, key, kind, fingerprint) != null)
			return result;
		if (session.Status is not (LibraryStockVerificationStatus.Counting or LibraryStockVerificationStatus.InReview) ||
			!cancelling && session.Status != LibraryStockVerificationStatus.Counting)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		if (session.ObservationCount != expectedObservationCount || session.Status != expectedStatus)
			throw new BusinessException(LibraryErrorCodes.StockVerificationPlanChanged);

		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		if (cancelling)
			session.Cancel(scope.ActorId, now, reason!);
		else
			session.StartReview(scope.ActorId, now);
		await sessions.UpdateAsync(session);
		await commandLog.InsertAsync(new(GuidGenerator.Create(), scope.TenantId, session.Id, null,
			scope.ActorId, kind, key, fingerprint, now));
		return result;
	}
}
