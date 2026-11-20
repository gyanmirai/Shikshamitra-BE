using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationProtectedReview(PreviewAcademicNotificationManualDto Request,
	NotificationSourceScopeDto[] Scopes);

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationReviewService(IRepository<NotificationSendReview, Guid> reviews,
	IAsyncQueryableExecuter queries, IDataProtectionProvider protection, INotificationPersonalWriteLock writeLock, IDataFilter filters) : ITransientDependency
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	internal async Task<NotificationSendReview?> CreateAsync(PreviewAcademicNotificationManualDto request,
		AcademicNotificationAccessContext context, AcademicNotificationManualPlan plan, DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (!plan.Partitions.SelectMany(x => x.Evaluation.Prepared).Any(x => x.ReviewFingerprint != null)) return null;
		var timeout = now.AddMinutes(10);
		var expiry = context.Source.ExpiresAt is DateTimeOffset sourceExpiry && sourceExpiry < timeout ? sourceExpiry : timeout;
		if (expiry <= now) return null;
		var review = new NotificationSendReview(Guid.NewGuid(), context.TenantId, context.UserId, Digest(request, context, plan), expiry);
		var scopes = plan.Partitions.Select(x => x.Source.Runtime.SourceScope).Distinct().ToArray();
		var sealedRequest = Protector(review).Protect(JsonSerializer.Serialize(new AcademicNotificationProtectedReview(request, scopes), Json));
		if (sealedRequest.Length > NotificationSendReview.MaximumProtectedRequestLength)
			throw new BusinessException(NotificationErrorCodes.ReviewInvalid);
		review.BindRequest(sealedRequest);
		return await reviews.InsertAsync(review, autoSave: true, cancellationToken: cancellationToken);
	}

	internal async Task<(NotificationSendReview Review, string IdempotencyHash)> LockConfirmationAsync(Guid tenantId, Guid actorId, ConfirmAcademicNotificationManualDto input,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (input.ReviewToken == Guid.Empty || string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
			input.IdempotencyKey.Length > 192 || input.IdempotencyKey != input.IdempotencyKey.Trim() || input.IdempotencyKey.Any(char.IsControl))
			throw new BusinessException(NotificationErrorCodes.ReviewInvalid);
		var keyHash = Hash(input.IdempotencyKey);
		await writeLock.AcquireAsync([$"manual-review:{input.ReviewToken:N}", $"manual-send:{actorId:N}:{keyHash}"], cancellationToken);
		var rows = await reviews.GetQueryableAsync();
		var review = await queries.SingleOrDefaultAsync(rows.Where(x => x.Id == input.ReviewToken && x.TenantId == tenantId &&
			x.ActorUserId == actorId && !x.IsDeleted), cancellationToken)
			?? throw new EntityNotFoundException(typeof(NotificationSendReview), input.ReviewToken);
		if (review.OperationId.HasValue)
		{
			if (review.IdempotencyKey != keyHash) throw new BusinessException(NotificationErrorCodes.ReviewConsumed);
			return (review, keyHash);
		}
		if (review.ExpiresAt <= now) throw new BusinessException(NotificationErrorCodes.ReviewExpired);
		using (filters.Disable<ISoftDelete>())
		{
			if (await queries.AnyAsync(rows.Where(x => x.TenantId == tenantId && x.ActorUserId == actorId &&
				x.IdempotencyKey == keyHash), cancellationToken))
				throw new BusinessException(NotificationErrorCodes.ReviewConsumed);
		}
		return (review, keyHash);
	}

	internal async Task<NotificationSendReview> LockDecisionAsync(Guid tenantId, Guid operationId, Guid approverId,
		CancellationToken cancellationToken)
	{
		if (operationId == Guid.Empty) throw new EntityNotFoundException(typeof(NotificationSendReview), operationId);
		await writeLock.AcquireAsync([$"manual-operation:{operationId:N}"], cancellationToken);
		return await RequireAssignedAsync(tenantId, operationId, approverId, cancellationToken);
	}

	internal async Task<IQueryable<NotificationSendReview>> AssignedAsync(Guid tenantId, Guid approverId) =>
		(await reviews.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted && x.OperationId != null &&
			x.AssignedApproverUserId == approverId && x.ActorUserId != approverId);

	internal async Task<bool> IsAssignedAsync(Guid tenantId, Guid operationId, Guid approverId, CancellationToken cancellationToken) =>
		await queries.AnyAsync((await AssignedAsync(tenantId, approverId)).Where(x => x.OperationId == operationId), cancellationToken);

	internal async Task<NotificationSendReview> RequireAssignedAsync(Guid tenantId, Guid operationId, Guid approverId,
		CancellationToken cancellationToken) =>
		await queries.SingleOrDefaultAsync((await AssignedAsync(tenantId, approverId)).Where(x => x.OperationId == operationId),
			cancellationToken) ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();

	internal PreviewAcademicNotificationManualDto ReadRequest(NotificationSendReview review) => ReadReview(review).Request;

	internal AcademicNotificationProtectedReview ReadReview(NotificationSendReview review)
	{
		try
		{
			if (review.ProtectedRequest is not { Length: > 0 and <= NotificationSendReview.MaximumProtectedRequestLength })
				throw new CryptographicException();
			var payload = Protector(review).Unprotect(review.ProtectedRequest);
			using var document = JsonDocument.Parse(payload);
			if (document.RootElement.TryGetProperty("request", out _))
			{
				var protectedReview = document.RootElement.Deserialize<AcademicNotificationProtectedReview>(Json);
				return protectedReview is { Request: not null, Scopes: not null } ? protectedReview : throw new JsonException();
			}
			var request = document.RootElement.Deserialize<PreviewAcademicNotificationManualDto>(Json) ?? throw new JsonException();
			return new(request, []);
		}
		catch (Exception error) when (error is CryptographicException or JsonException or FormatException or InvalidOperationException)
		{
			throw new BusinessException(NotificationErrorCodes.ReviewChanged);
		}
	}

	internal Task SaveAsync(NotificationSendReview review, CancellationToken cancellationToken) =>
		reviews.UpdateAsync(review, autoSave: true, cancellationToken: cancellationToken);

	private IDataProtector Protector(NotificationSendReview review) => protection.CreateProtector("Academic.Notification.Review.v1",
		review.TenantId!.Value.ToString("N"), review.ActorUserId.ToString("N"), review.Id.ToString("N"));

	internal static string Digest(PreviewAcademicNotificationManualDto request, AcademicNotificationAccessContext context,
		AcademicNotificationManualPlan plan) => Hash(new
	{
		SchemaVersion = 1,
		context.TenantId, context.UserId, request.EventKey, request.SourceType, request.SourceId, request.TemplateId,
		context.Source.SourceRevision, SourceExpiry = context.Source.ExpiresAt?.ToUniversalTime(), plan.RequiresApproval,
		SelectedRecipients = request.SelectedRecipients.Select(Hash).Order(StringComparer.Ordinal),
		Channels = request.Channels.Order(),
		Partitions = plan.Partitions.Select(x => Hash(new
		{
			x.Source.Runtime, x.RuleId, x.RuleRevision, x.TemplateRevisions, x.Source.CapturedRecipients,
			Decisions = x.Evaluation.Prepared.Select(row => Hash(new
			{
				row.Recipient.SourceRecipient, row.Recipient.Recipient, row.Recipient.AssociationRevision,
				row.Recipient.Channel, row.Language, row.ExclusionCode, row.ReviewFingerprint
			})).Order(StringComparer.Ordinal)
		})).Order(StringComparer.Ordinal)
	});

	private static string Hash<T>(T value) =>
		Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
}
