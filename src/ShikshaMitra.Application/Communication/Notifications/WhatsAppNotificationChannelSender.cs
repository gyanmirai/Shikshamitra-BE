using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationChannelSender))]
public sealed class WhatsAppNotificationChannelSender(IUnitOfWorkManager units, WhatsAppProviderRoute routes,
	WhatsAppApprovedTemplates approvals, IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter queries,
	WhatsAppTemplateCompiler compiler, WhatsAppProviderClient client) : INotificationChannelSender, ITransientDependency
{
	public CommunicationChannelType Channel => CommunicationChannelType.WhatsApp;

	public async Task<NotificationChannelSendResult> SendAsync(NotificationChannelSendRequest input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (input.Channel != Channel || input.ProviderSettingId == Guid.Empty) return Reject(NotificationErrorCodes.ProviderUnavailable);
		if (input.TemplateRevisionId is not Guid revisionId || input.WhatsAppTemplateBindingId is not Guid bindingId ||
			input.WhatsAppVariables == null || input.Recipient == null) return Reject(NotificationErrorCodes.TemplateInvalid);

		WhatsAppProviderConnection connection;
		WhatsAppApprovedTemplateMessage message;
		using (var read = units.Begin(requiresNew: true, isTransactional: false))
		{
			var provider = await routes.FindAsync(cancellationToken, input.ProviderSettingId);
			if (provider == null) return Reject(NotificationErrorCodes.ProviderUnavailable);
			var approved = await approvals.LoadAsync(provider, [revisionId], cancellationToken);
			if (!approved.TryGetValue(revisionId, out var binding) || binding.Id != bindingId ||
				!WhatsAppApprovedTemplates.MatchesLanguage(binding, input.LanguageCode)) return Reject("WhatsAppTemplateNotApproved");
			var revision = await queries.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x =>
				x.Id == revisionId && x.TenantId == binding.TenantId && x.IsPublished && !x.IsDeleted &&
				x.Channel == Channel && x.RecipientKind == input.Recipient.Kind), cancellationToken);
			if (revision == null || !string.Equals(revision.LanguageCode, input.LanguageCode, StringComparison.OrdinalIgnoreCase))
				return Reject(NotificationErrorCodes.TemplateInvalid);
			connection = provider.Connection;
			var mapping = WhatsAppApprovedTemplates.ReadParameters(binding, connection);
			if (mapping == null) return Reject(NotificationErrorCodes.TemplateInvalid);
			try
			{
				var components = compiler.Bind(revision, mapping, input.WhatsAppVariables);
				var language = compiler.Compile(revision).ProviderLanguage;
				message = new(input.Destination, binding.ProviderTemplateName, language, components);
			}
			catch (BusinessException error) when (error.Code is NotificationErrorCodes.TemplateInvalid or NotificationErrorCodes.RequiredVariableMissing)
			{
				return Reject(error.Code!);
			}
			await read.CompleteAsync(cancellationToken);
		}
		return await client.SendAsync(connection, message, cancellationToken);
	}

	private static NotificationChannelSendResult Reject(string code) => new(NotificationProviderAcceptance.Rejected, null, code);
}
