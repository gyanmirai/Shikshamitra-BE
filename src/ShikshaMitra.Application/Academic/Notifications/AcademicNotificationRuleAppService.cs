using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationRuleAppService(IRepository<IdentityUser, Guid> users, IUserScopeAccessService scopes,
	IPermissionChecker permissions, AcademicNotificationCatalogue catalogue, AcademicNotificationRuleScopeAccess scopeAccess,
	NotificationEffectiveRuleResolver effectiveRules, IRepository<NotificationRule, Guid> rules,
	NotificationRuleConfigurationCodec codec, AcademicNotificationRuleTemplates templates, INotificationPersonalWriteLock writeLock)
	: ApplicationService, IAcademicNotificationRuleAppService
{
	public virtual async Task<PagedResultDto<AcademicNotificationRuleListItemDto>> GetListAsync(GetAcademicNotificationRulesDto input,
		CancellationToken cancellationToken = default)
	{
		var (tenant, _, callerScope) = await AcademicNotificationIdentityAccess.RequireAsync(CurrentTenant, CurrentUser, users, scopes,
			permissions, ShikshaMitraSubmodulePermissions.Academics.Notification.View, cancellationToken);
		var requested = new AcademicNotificationScopeDto(input.AcademicYearId, input.BoardId, input.MediumId, input.ClassId, input.DivisionId);
		var scope = await scopeAccess.RequireAsync(requested, tenant, callerScope, cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100)
			throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);

		IReadOnlyList<NotificationEventDefinition> definitions = string.IsNullOrWhiteSpace(input.EventKey)
			? catalogue.GetAll() : [catalogue.Get(input.EventKey)];
		var page = definitions.OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.EventKey, StringComparer.Ordinal)
			.Skip(input.SkipCount).Take(input.MaxResultCount).ToArray();
		var matches = await effectiveRules.ResolveManyAsync(page.Select(x => x.EventKey).ToArray(), scope, cancellationToken);
		return new(definitions.Count, page.Select(definition =>
		{
			var row = matches.GetValueOrDefault(definition.EventKey);
			return new AcademicNotificationRuleListItemDto(definition.EventKey, requested,
				row == null ? null : Map(row, definition), row != null && row.ScopeKey != scope.Key);
		}).ToArray());
	}

	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationRuleDto> SaveAsync(string eventKey, SaveAcademicNotificationRuleDto input,
		CancellationToken cancellationToken = default)
	{
		var (tenant, _, callerScope) = await AcademicNotificationIdentityAccess.RequireAsync(CurrentTenant, CurrentUser, users, scopes,
			permissions, ShikshaMitraSubmodulePermissions.Academics.Notification.Add, cancellationToken);
		var definition = catalogue.Get(eventKey);
		if (eventKey != input.EventKey) throw new BusinessException(NotificationErrorCodes.EventUnknown);
		var scope = await scopeAccess.RequireAsync(input.Scope, tenant, callerScope, cancellationToken);
		var configuration = BuildConfiguration(input);
		var json = codec.Serialize(configuration, definition, allowNoTemplates: !input.Enabled);

		await writeLock.AcquireAsync([$"academic-rule:{eventKey}:{scope.Key}"], cancellationToken);
		var row = await AsyncExecuter.SingleOrDefaultAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == tenant && !x.IsDeleted && x.EventKey == eventKey && x.ScopeKey == scope.Key), cancellationToken);
		if (row == null ? !string.IsNullOrEmpty(input.ConcurrencyStamp) : row.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new AbpDbConcurrencyException();

		if (input.Enabled) catalogue.EnsureAvailable(eventKey);
		await templates.EnsureAsync(tenant, scope, definition, configuration, input.Enabled, cancellationToken);

		var isNew = row == null;
		row ??= new NotificationRule(GuidGenerator.Create(), tenant, eventKey, scope);
		var revision = row.Revision;
		row.Configure(input.Enabled, json, row.ConcurrencyStamp);
		if (isNew) await rules.InsertAsync(row, autoSave: true, cancellationToken: cancellationToken);
		else if (row.Revision != revision) await rules.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
		return Map(row, definition);
	}

	private AcademicNotificationRuleDto Map(NotificationRule row, NotificationEventDefinition definition) =>
		new(row.Id, row.EventKey, new(row.AcademicYearId, row.BoardId, row.MediumId, row.ClassId, row.DivisionId),
			row.IsEnabled, row.Revision, row.ConcurrencyStamp,
			!row.IsEnabled && row.ConfigurationJson == "{}" ? null :
			codec.Deserialize(row.ConfigurationJson, definition, allowNoTemplates: !row.IsEnabled));

	private static NotificationRuleRuntimeConfiguration BuildConfiguration(SaveAcademicNotificationRuleDto input)
	{
		if (input.Timing == null || input.QuietHours == null) throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);
		var timing = input.Timing;
		var quiet = input.QuietHours;
		var defaults = NotificationRuleTimingConfiguration.Default;
		return new(1, input.GuardianMode, input.AdditionalGuardianRelationships, input.IncludeStudentAccount,
			input.StaffAudienceSelectors, input.Channels, input.TemplateIds,
			new(timing.DelayMinutes, timing.ReminderSchoolDaysBefore, timing.ReminderLocalTime ?? defaults.ReminderLocalTime,
				quiet.Enabled, quiet.Start ?? defaults.QuietStart, quiet.End ?? defaults.QuietEnd, quiet.Bypass), input.FallbackLanguage);
	}
}
