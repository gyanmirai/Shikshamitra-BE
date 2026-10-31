using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Students.Portal;

/// <summary>Checks persisted portal authority without changing the acting principal or granting staff scope.</summary>
[RemoteService(false)]
public class StudentPortalPhotoAuthority(StudentPortalAccessService portal, StudentRecordsAccessService records,
	IStudentRepository students, IRepository<IdentityUser, Guid> users, IdentityUserManager userManager,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	internal async Task<StudentRecordsAccess> EnsureAsync(Guid studentId, bool write, Guid? requester = null)
	{
		var actor = requester ?? CurrentUser.Id ?? throw new AbpAuthorizationException();
		if (!(await portal.GetStudentsForUserAsync(actor)).Any(x => x.StudentId == studentId)) throw new AbpAuthorizationException();
		var user = await users.GetAsync(x => x.Id == actor && x.TenantId == CurrentTenant.Id);
		var roles = await userManager.GetRolesAsync(user);
		var claims = new[] { new Claim(AbpClaimTypes.UserId, actor.ToString()), new Claim(AbpClaimTypes.TenantId, CurrentTenant.Id!.Value.ToString()) }
			.Concat(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "PortalPhotoAuthority"));
		if (!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View) ||
			write && !await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add)) throw new AbpAuthorizationException();
		var student = await students.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id) ?? throw new AbpAuthorizationException();
		var allowed = await records.EvaluateEligibilityAsync(student);
		if (StudentRecordVisibility.IsMasked(allowed.Policy, "photo", roles)) throw new AbpAuthorizationException();
		if (write && allowed.Access != "Full") throw new BusinessException("Students:Records:ReadOnly");
		return allowed;
	}
}
