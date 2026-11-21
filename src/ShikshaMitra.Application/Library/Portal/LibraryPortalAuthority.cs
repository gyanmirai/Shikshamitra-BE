using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library.Portal;

[RemoteService(false)]
public class LibraryPortalAuthority(StudentPortalAccessService portal, LibraryReservationEligibilityReader eligibility,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	internal async Task<Guid> RequireSchoolAsync(string action)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id is null || CurrentUser.Id == Guid.Empty ||
			CurrentTenant.Id is not Guid tenant || tenant == Guid.Empty || CurrentUser.TenantId != tenant ||
			!await permissions.IsGrantedAsync(Actions.View) || action != Actions.View && !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException();
		return tenant;
	}

	internal async Task<LibraryPortalOwner> ResolveAsync(Guid id, string action)
	{
		var tenant = await RequireSchoolAsync(action);
		var student = await portal.ResolveStudentAsync(id);
		var facts = (await eligibility.ReadAsync([student.Id])).GetValueOrDefault(student.Id);
		if (student.TenantId != tenant || facts?.PersonId != id || facts.Role != 0) throw new AbpAuthorizationException();
		return new(tenant, CurrentUser.Id!.Value, id, facts, facts.MemberIds.Distinct().ToArray());
	}
}

internal sealed record LibraryPortalOwner(Guid Tenant, Guid Actor, Guid StudentId, LibraryReservationBorrowerFacts Facts, Guid[] Aliases);
