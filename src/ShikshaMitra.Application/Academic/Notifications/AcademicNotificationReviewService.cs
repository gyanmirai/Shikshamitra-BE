using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationReviewService(IRepository<NotificationSendReview, Guid> reviews) : ITransientDependency
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
		return await reviews.InsertAsync(review, autoSave: true, cancellationToken: cancellationToken);
	}

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
