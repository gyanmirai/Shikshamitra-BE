using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationTemplateRevisionService
{
	public virtual async Task<int> InitializeDefaultsAsync(string eventKey, CancellationToken cancellationToken = default)
	{
		var tenantId = RequireTenant();
		var definition = validator.GetDefinition(eventKey);
		await AuthorizeAsync(eventKey, null, NotificationTemplateAction.Write, cancellationToken);
		await writeLock.AcquireAsync([$"notification-template-defaults:{eventKey}"], cancellationToken);
		var variants = definition.RecipientSelectors.Select(x => x is "PrimaryGuardian" or "AdditionalGuardians" ? "Guardian" : x)
			.Distinct(StringComparer.Ordinal).SelectMany(kind => definition.SupportedChannels.Select(channel => new {
				Kind = kind, Channel = channel, Id = DefaultId($"{tenantId:N}:{eventKey}:{kind}:{(int)channel}:en")
			})).ToArray();
		var ids = variants.Select(x => x.Id).ToArray();
		List<Guid> existing;
		using (filters.Disable<ISoftDelete>())
			existing = await executer.ToListAsync((await headers.GetQueryableAsync())
				.Where(x => x.TenantId == tenantId && Enumerable.Contains(ids, x.Id)).Select(x => x.Id), cancellationToken);
		var pending = new List<(CommunicationTemplate Header, CommunicationTemplateRevision Revision, string Name)>();
		foreach (var variant in variants.Where(x => !existing.Contains(x.Id)))
		{
			var content = defaults.Create(definition, variant.Kind, variant.Channel, "en");
			var header = new CommunicationTemplate(variant.Id, tenantId);
			var revision = new CommunicationTemplateRevision(DefaultId($"{variant.Id:N}:1"), tenantId, header.Id, 1,
				eventKey, variant.Kind, variant.Channel, "en", content.Subject, NormalizeBody(variant.Channel, content.Body), definition.SchemaVersion);
			Validate(revision);
			pending.Add((header, revision, $"{eventKey} / {variant.Kind} / {variant.Channel} / en"));
		}
		foreach (var (header, revision, name) in pending)
		{
			await headers.InsertAsync(header, autoSave: true, cancellationToken: cancellationToken);
			await revisions.InsertAsync(revision, autoSave: true, cancellationToken: cancellationToken);
			ApplyHeader(header, revision, name);
			await headers.UpdateAsync(header, autoSave: true, cancellationToken: cancellationToken);
		}
		return pending.Count;
	}

	// Stable IDs retain the deletion decision when defaults are initialized again.
	private static Guid DefaultId(string variant) => new(SHA256.HashData(Encoding.UTF8.GetBytes("ShikshaMitra.Notification.Default.v1:" + variant)).AsSpan(0, 16));
}
