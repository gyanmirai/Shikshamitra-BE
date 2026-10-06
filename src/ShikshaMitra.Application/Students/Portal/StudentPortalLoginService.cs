using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

/// <summary>Portal-only login eligibility; does not change staff authentication rules.</summary>
[RemoteService(false)]
public class StudentPortalLoginService(IdentityUserManager users, IRepository<IdentityUser, Guid> identities,
	IRepository<StudentPortalAccountLink, Guid> links, IRepository<Student, Guid> students, IDataFilter dataFilter)
	: ApplicationService, ITransientDependency
{
	public async Task<bool> IsPortalUserAsync(IdentityUser user)
	{
		var roles = await users.GetRolesAsync(user);
		return roles.Count == 1 && roles[0] is "Student" or "Parent";
	}

	public async Task<string?> FindMobileUsernameAsync(string? identifier)
	{
		if (!CurrentTenant.Id.HasValue || string.IsNullOrWhiteSpace(identifier)) return null;
		var phone = identifier.Trim();
		if (!phone.All(c => char.IsDigit(c) || c == '+')) return null;
		var matches = await identities.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.PhoneNumber == phone);
		// Shared numbers never choose an arbitrary identity, including a staff identity.
		if (matches.Count != 1) return null;
		var user = matches[0];
		return user.IsActive && !string.IsNullOrWhiteSpace(user.Email) && await IsPortalUserAsync(user) ? user.UserName : null;
	}

	public async Task<bool> CanAccessAsync(IdentityUser user)
	{
		if (!CurrentTenant.Id.HasValue || user.TenantId != CurrentTenant.Id || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return false;
		var studentQuery = await students.GetQueryableAsync();
		var linkQuery = await links.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(from link in linkQuery
			join student in studentQuery on link.StudentId equals student.Id
			where link.TenantId == CurrentTenant.Id && student.TenantId == CurrentTenant.Id && link.UserId == user.Id &&
				student.Status != StudentLifeCycleStatusEnum.Left &&
				(link.AccountType == "guardian" || link.AccountType == "student" && link.Relationship == "self")
			select link.Id)) return true;
		var legacy = await students.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id && x.Status != StudentLifeCycleStatusEnum.Left);
		if (legacy.Count == 0) return false;
		using (dataFilter.Disable<ISoftDelete>())
		{
			var history = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id && x.AccountType == "student" && x.Relationship == "self");
			return legacy.Any(student => history.All(link => link.StudentId != student.Id));
		}
	}
}
