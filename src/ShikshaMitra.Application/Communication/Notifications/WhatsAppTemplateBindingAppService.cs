using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(IWhatsAppTemplateBindingAppService))]
public sealed class WhatsAppTemplateBindingAppService(ICurrentTenant tenant, ICurrentUser user, IPermissionChecker permissions,
	IUnitOfWorkManager units, IAmbientUnitOfWork ambient, INotificationPersonalWriteLock writeLock, IRepository<WhatsAppTemplateBinding, Guid> bindings,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter queries, IDataFilter filters, IGuidGenerator guids,
	IClock clock, NotificationTemplateRevisionService templates, WhatsAppProviderRoute routes, WhatsAppTemplateCompiler compiler,
	WhatsAppProviderClient client, NotificationTemplateVariableValidator validator, IConfiguration configuration)
	: IWhatsAppTemplateBindingAppService, ITransientDependency
{
	private const string ViewPermission = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.View;
	private const string WritePermission = ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add;

	public async Task<WhatsAppTemplateBindingDto?> GetAsync(Guid templateId, Guid revisionId, Guid providerSettingId,
		CancellationToken cancellationToken = default)
	{
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var tenantId = await RequirePermissionAsync(ViewPermission);
		var (_, revision) = await templates.LoadCommandAsync(templateId, revisionId, NotificationTemplateAction.Read, cancellationToken);
		EnsureWhatsAppRevision(revision);
		var row = await FindBindingAsync(tenantId, revisionId, providerSettingId, cancellationToken);
		await read.CompleteAsync(cancellationToken);
		return row == null ? null : Map(row, templateId);
	}

	public async Task<WhatsAppTemplateBindingDto> CreateAsync(CreateWhatsAppTemplateBindingDto input, CancellationToken cancellationToken = default)
	{
		using var write = units.Begin(requiresNew: true, isTransactional: true);
		var tenantId = await RequirePermissionAsync(WritePermission);
		var (_, revision) = await templates.LoadCommandAsync(input.TemplateId, input.RevisionId, NotificationTemplateAction.Write, cancellationToken);
		if (!WhatsAppProviderClient.ValidTemplateName(input.ProviderTemplateName) || revision.IsRetired)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var compiled = compiler.Compile(revision);
		await LockAsync(input.RevisionId, input.ProviderSettingId, cancellationToken);
		var provider = await RequireProviderAsync(input.ProviderSettingId, cancellationToken);
		var parameters = WhatsAppApprovedTemplates.Encode(compiled, compiler, provider.Connection);
		WhatsAppTemplateBinding? row;
		using (filters.Disable<ISoftDelete>())
			row = await FindBindingAsync(tenantId, revision.Id, provider.Id, cancellationToken, includeDeleted: true);
		if (row == null)
		{
			row = new(guids.Create(), tenantId, revision.Id, provider.Id, input.ProviderTemplateName, compiled.ProviderLanguage, parameters);
			await bindings.InsertAsync(row, autoSave: true, cancellationToken: cancellationToken);
		}
		else if (row.IsDeleted || row.ProviderTemplateName != input.ProviderTemplateName || row.ParameterMappingsJson != parameters)
			throw new BusinessException(NotificationErrorCodes.StateConflict);
		await write.CompleteAsync(cancellationToken);
		return Map(row, input.TemplateId);
	}

	public Task<WhatsAppTemplateBindingDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default) =>
		RefreshAsync(id, submit: true, cancellationToken);

	public Task<WhatsAppTemplateBindingDto> SyncAsync(Guid id, CancellationToken cancellationToken = default) =>
		RefreshAsync(id, submit: false, cancellationToken);

	private async Task<WhatsAppTemplateBindingDto> RefreshAsync(Guid id, bool submit, CancellationToken cancellationToken)
	{
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		// ABP repository interceptors can activate the reserved request unit even inside a requires-new read.
		var reserved = ambient.UnitOfWork;
		ambient.SetUnitOfWork(null);
		try
		{
			return await RefreshOutsideRequestUnitAsync(id, submit, cancellationToken);
		}
		finally
		{
			ambient.SetUnitOfWork(reserved);
		}
	}

	private async Task<WhatsAppTemplateBindingDto> RefreshOutsideRequestUnitAsync(Guid id, bool submit, CancellationToken cancellationToken)
	{
		RefreshTarget target;
		using (var read = units.Begin(requiresNew: true, isTransactional: false))
		{
			target = await LoadTargetAsync(id, cancellationToken);
			if (submit && target.Revision.IsRetired) throw new BusinessException(NotificationErrorCodes.TemplateImmutable);
			await read.CompleteAsync(cancellationToken);
		}
		var observedAt = new DateTimeOffset(clock.Now.ToUniversalTime());
		var observation = await ObserveAsync(target, submit, cancellationToken);

		using var write = units.Begin(requiresNew: true, isTransactional: true);
		await LockAsync(target.Binding.TemplateRevisionId, target.Binding.ProviderSettingId, cancellationToken);
		var current = await LoadTargetAsync(id, cancellationToken);
		if (current.Binding.LastSyncedAt is DateTimeOffset lastSynced && lastSynced >= observedAt)
			return Map(current.Binding, current.Revision.TemplateId);
		var result = Reconcile(current.Binding, observation);
		if (result != null)
		{
			current.Binding.RecordProviderStatus(result.Id, result.Status, observedAt);
			await bindings.UpdateAsync(current.Binding, autoSave: true, cancellationToken: cancellationToken);
		}
		await write.CompleteAsync(cancellationToken);
		return Map(current.Binding, current.Revision.TemplateId);
	}

	private async Task<ProviderObservation> ObserveAsync(RefreshTarget target, bool submit, CancellationToken cancellationToken)
	{
		var binding = target.Binding;
		var match = await client.FindTemplateAsync(target.Connection, binding.ProviderTemplateName, target.Compiled.ProviderLanguage, cancellationToken);
		if (match != null || !submit || binding.ProviderTemplateId != null)
			return new(match, match?.Matches(target.Compiled) == true, null);

		var definition = validator.GetDefinition(target.Revision.EventKey);
		var examples = NotificationTemplatePreviewService.CreateExampleVariables(definition, configuration["App:ClientUrl"])
			.ToDictionary(x => x.Key, x => x.Value.Value!, StringComparer.Ordinal);
		var receipt = await client.SubmitTemplateAsync(target.Connection, binding.ProviderTemplateName, target.Compiled, examples, cancellationToken);
		match = await client.FindTemplateAsync(target.Connection, binding.ProviderTemplateName, target.Compiled.ProviderLanguage, cancellationToken);
		return new(match, match?.Matches(target.Compiled) == true && match.Id == receipt.Id, receipt);
	}

	private static WhatsAppTemplateSubmission? Reconcile(WhatsAppTemplateBinding binding, ProviderObservation observation)
	{
		var pinnedId = binding.ProviderTemplateId ?? observation.Receipt?.Id;
		if (observation.Match is { } match && observation.Exact && (pinnedId == null || pinnedId == match.Id))
			return new(match.Id, match.Status);
		if (observation.Match == null && binding.ProviderTemplateId == null && observation.Receipt is { } receipt)
			return receipt.Status == NotificationProviderTemplateState.Approved
				? receipt with { Status = NotificationProviderTemplateState.PendingApproval } : receipt;
		if (pinnedId != null) return new(pinnedId, NotificationProviderTemplateState.Disabled);
		if (observation.Match != null) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		return null;
	}

	private async Task<RefreshTarget> LoadTargetAsync(Guid id, CancellationToken cancellationToken)
	{
		var tenantId = await RequirePermissionAsync(WritePermission);
		var row = await queries.FirstOrDefaultAsync((await bindings.GetQueryableAsync()).Where(x =>
			x.Id == id && x.TenantId == tenantId && !x.IsDeleted), cancellationToken)
			?? throw new EntityNotFoundException(typeof(WhatsAppTemplateBinding), id);
		var templateId = await queries.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x =>
			x.Id == row.TemplateRevisionId && x.TenantId == tenantId && !x.IsDeleted).Select(x => (Guid?)x.TemplateId), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationTemplateRevision), row.TemplateRevisionId);
		var (_, revision) = await templates.LoadCommandAsync(templateId, row.TemplateRevisionId, NotificationTemplateAction.Write, cancellationToken);
		var compiled = compiler.Compile(revision);
		var provider = await RequireProviderAsync(row.ProviderSettingId, cancellationToken);
		if (WhatsAppApprovedTemplates.ReadParameters(row, provider.Connection) != compiler.EncodeParameters(compiled) ||
			!WhatsAppApprovedTemplates.MatchesLanguage(row, compiled.ProviderLanguage))
			throw new BusinessException(NotificationErrorCodes.ProviderUnavailable);
		return new(row, revision, compiled, provider.Connection);
	}

	private async Task<WhatsAppTemplateBinding?> FindBindingAsync(Guid tenantId, Guid revisionId, Guid providerSettingId,
		CancellationToken cancellationToken, bool includeDeleted = false) =>
		await queries.FirstOrDefaultAsync((await bindings.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.TemplateRevisionId == revisionId && x.ProviderSettingId == providerSettingId && (includeDeleted || !x.IsDeleted)), cancellationToken);

	private async Task<NotificationWhatsAppProvider> RequireProviderAsync(Guid providerSettingId, CancellationToken cancellationToken) =>
		await routes.FindAsync(cancellationToken, providerSettingId) ?? throw new BusinessException(NotificationErrorCodes.ProviderUnavailable);

	private Task LockAsync(Guid revisionId, Guid providerSettingId, CancellationToken cancellationToken) =>
		writeLock.AcquireAsync([$"whatsapp-template:{revisionId:N}:{providerSettingId:N}"], cancellationToken);

	private async Task<Guid> RequirePermissionAsync(string permission)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || !user.IsAuthenticated || user.Id == null || user.TenantId != tenantId ||
			!await permissions.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
		return tenantId;
	}

	private static void EnsureWhatsAppRevision(CommunicationTemplateRevision revision)
	{
		if (revision.Channel != CommunicationChannelType.WhatsApp || !revision.IsPublished)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
	}

	private static WhatsAppTemplateBindingDto Map(WhatsAppTemplateBinding row, Guid templateId) =>
		new(row.Id, templateId, row.TemplateRevisionId, row.ProviderSettingId, row.ProviderTemplateName, row.LanguageCode,
			row.ProviderTemplateId, row.ProviderStatus, row.LastSyncedAt);

	private sealed record RefreshTarget(WhatsAppTemplateBinding Binding, CommunicationTemplateRevision Revision,
		WhatsAppCompiledTemplate Compiled, WhatsAppProviderConnection Connection);
	private sealed record ProviderObservation(WhatsAppProviderTemplate? Match, bool Exact, WhatsAppTemplateSubmission? Receipt);
}
