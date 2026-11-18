using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
	WebPushKeyConfiguration push) : ApplicationService, IAcademicNotificationCatalogueAppService
{
	public virtual async Task<AcademicNotificationCatalogueDto> GetAsync(CancellationToken cancellationToken = default)
	{
		await AcademicNotificationIdentityAccess.RequireAsync(CurrentTenant, CurrentUser, users, scopes, permissions,
			ShikshaMitraSubmodulePermissions.Academics.Notification.View, cancellationToken);
		var events = catalogue.GetAll().Select(x => new AcademicNotificationCatalogueEventDto(x, catalogue.GetAvailability(x.EventKey))).ToArray();
		return new(events, [
			new(CommunicationChannelType.InApp, true),
			new(CommunicationChannelType.Email, await email.FindAsync(cancellationToken) != null),
			new(CommunicationChannelType.WhatsApp, await whatsApp.FindAsync(cancellationToken) != null),
			new(CommunicationChannelType.WebPush, push.Read() != null)
		]);
	}
}
