using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class SeatingNotificationSourceAuthorizer(ExamSeatAllocationAppService seating,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => SeatingNotificationSource.SupportedEvents;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (eventKey != SeatingNotificationSource.Published || sourceId == Guid.Empty || tenant.Id is not Guid school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View))
			throw new AbpAuthorizationException();
		try
		{
			var exam = await seating.GetScopedExamAsync(sourceId);
			if (exam.TenantId != school || exam.IsDeleted) throw new AbpAuthorizationException();
		}
		catch (EntityNotFoundException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}
