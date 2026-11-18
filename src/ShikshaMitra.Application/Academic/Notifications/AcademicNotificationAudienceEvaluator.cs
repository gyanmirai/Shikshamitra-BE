using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationAudienceEvaluation(string? BlockingIssue,
	IReadOnlyList<NotificationRecipientDispatchDecision> Decisions, IReadOnlyList<NotificationPreparedDispatch> Prepared);

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationAudienceEvaluator(IEnumerable<INotificationRuntimeRecipientResolver> resolvers,
	NotificationDispatchPolicy policy, NotificationDispatchMaterializer materializer, NotificationEmailRoute emailRoute) : ITransientDependency
{
	internal Task<NotificationEmailProvider?> FindEmailProviderAsync(IReadOnlyList<CommunicationChannelType> channels,
		CancellationToken cancellationToken) => channels.Contains(CommunicationChannelType.Email)
		? emailRoute.FindAsync(cancellationToken) : Task.FromResult<NotificationEmailProvider?>(null);

	internal async Task<AcademicNotificationAudienceEvaluation> EvaluateAsync(NotificationCurrentSource source,
		IReadOnlyList<Guid> pins, NotificationEmailProvider? email, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var matching = resolvers.Where(x => x.Supports(source.Definition.EventKey)).Take(2).ToArray();
		if (matching.Length != 1) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var candidates = await matching[0].ResolveAsync(source, cancellationToken);
		var configuration = source.Runtime.Configuration;
		var result = await policy.EvaluatePreviewAsync(source, candidates, now, now.AddMinutes(configuration.Timing.DelayMinutes),
			email?.ConsentRequired == true, cancellationToken);
		if (result.ExclusionCode != null) return new(result.ExclusionCode, [], []);
		var prepared = pins.Count > 0
			? await materializer.PreparePreviewAsync(source, result, email, pins, cancellationToken)
			: result.Recipients.Select(x =>
				new NotificationPreparedDispatch(x.Recipient, x.LanguageCode, null, x.ExclusionCode ?? NotificationErrorCodes.TemplateInvalid)).ToArray();
		return new(null, result.Recipients, prepared);
	}
}
