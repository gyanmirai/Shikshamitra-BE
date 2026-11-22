using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationCatalogueAppService(IRepository<IdentityUser, Guid> users,
	IUserScopeAccessService scopes, IPermissionChecker permissions,
	AcademicNotificationCatalogue catalogue, NotificationEmailRoute email, WhatsAppProviderRoute whatsApp,
	WebPushKeyConfiguration push, IConfiguration configuration) : ApplicationService, IAcademicNotificationCatalogueAppService
{
	public virtual async Task<AcademicNotificationCatalogueDto> GetAsync(CancellationToken cancellationToken = default)
	{
		var (tenantId, _, _) = await AcademicNotificationIdentityAccess.RequireAsync(CurrentTenant, CurrentUser, users, scopes, permissions,
			ShikshaMitraSubmodulePermissions.Academics.Notification.View, cancellationToken);
		var events = catalogue.GetAll().Select(definition =>
		{
			var key = definition.EventKey;
			var availability = catalogue.GetAvailability(key);
			return new AcademicNotificationCatalogueEventDto(definition, availability, catalogue.GetManualAvailability(key))
			{
				AvailabilityDetail = availability.IsAvailable ? null : AcademicNotificationCatalogue.GetSourceDependency(key)
			};
		}).ToArray();
		return new(events, [
			new(CommunicationChannelType.InApp, true),
			new(CommunicationChannelType.Email, await email.FindAsync(cancellationToken) != null),
			new(CommunicationChannelType.WhatsApp, await whatsApp.FindAsync(cancellationToken) != null),
			new(CommunicationChannelType.WebPush, push.Read() != null)
		])
		{
			IsQueueWorkerEnabled = NotificationQueueWorkerConfiguration.IsEnabled(configuration),
			IsSchoolQueuePaused = NotificationQueueWorkerConfiguration.IsSchoolPaused(configuration, tenantId),
			PausedChannels = NotificationQueueWorkerConfiguration.ReadPausedChannels(configuration, tenantId)
		};
	}
}
