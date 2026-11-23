using System;
using System.Collections.Generic;
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

[RemoteService(false)]
public class StudentAppAccessEnforcement(
	IRepository<StudentAppAccessRestriction, Guid> restrictions,
	IRepository<IdentityUser, Guid> identities,
	IdentityUserManager users,
	IRepository<StudentPortalAccountLink, Guid> links,
	IStudentRepository students,
	IDataFilter dataFilter) : ApplicationService, ITransientDependency
{
	public const string SessionClaim = "shikshamitra_portal_session";
	public const string OffMessage = "App access is off. Turn it on in Student Records before sending login details.";
	private const string SessionRevisionProperty = "ShikshaMitraPortalSessionRevision";

	public static string? GetSessionRevision(IdentityUser user) => user.GetProperty<string>(SessionRevisionProperty);

	public async Task<bool> IsPortalUserAsync(IdentityUser user)
	{
		var roles = await users.GetRolesAsync(user);
		return roles.Count == 1 && roles[0] is "Student" or "Parent";
	}

	internal async Task<IQueryable<StudentAppAccessRestriction>> GetOffRestrictionsAsync(Guid? tenantId) =>
		(await restrictions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.IsOff);

	internal async Task<bool> IsRecipientOffAsync(Guid studentId, string accountType, string? relationship, Guid? userId)
	{
		if (!CurrentTenant.Id.HasValue || accountType != "student" &&
			(relationship is not ("father" or "mother") || !userId.HasValue)) return false;
		var recipient = accountType == "student" ? null : userId;
		return await AsyncExecuter.AnyAsync((await GetOffRestrictionsAsync(CurrentTenant.Id))
			.Where(x => x.StudentId == studentId && x.UserId == recipient));
	}

	public async Task<bool> HasAvailableAssociationAsync(IdentityUser user)
	{
		var tenantId = CurrentTenant.Id;
		if (!tenantId.HasValue || user.TenantId != tenantId || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return false;

		var studentQuery = await students.GetQueryableWithoutAutoIncludesAsync();
		var linkQuery = await links.GetQueryableAsync();
		var off = await GetOffRestrictionsAsync(tenantId);
		if (await AsyncExecuter.AnyAsync(from link in linkQuery
			join student in studentQuery on link.StudentId equals student.Id
			where link.TenantId == tenantId && student.TenantId == tenantId && link.UserId == user.Id &&
				student.Status != StudentLifeCycleStatusEnum.Left &&
				(link.AccountType == "guardian" || link.AccountType == "student" && link.Relationship == "self") &&
				!off.Any(x => x.StudentId == student.Id &&
					(link.AccountType == "student" && x.UserId == null ||
						(link.Relationship == "father" || link.Relationship == "mother") && x.UserId == user.Id))
			select link.Id)) return true;
		var legacy = await AsyncExecuter.ToListAsync(studentQuery
			.Where(x => x.TenantId == tenantId && x.UserId == user.Id && x.Status != StudentLifeCycleStatusEnum.Left &&
				!off.Any(r => r.StudentId == x.Id && r.UserId == null))
			.Select(x => x.Id));
		if (legacy.Count == 0) return false;

		using (dataFilter.Disable<ISoftDelete>())
		{
			var history = await AsyncExecuter.ToListAsync((await links.GetQueryableAsync())
				.Where(x => x.TenantId == tenantId && x.UserId == user.Id && x.AccountType == "student" && x.Relationship == "self")
				.Select(x => x.StudentId));
			return legacy.Any(studentId => !history.Contains(studentId));
		}
	}

	// Call after persisting the restriction in the same transaction as the Records change.
	public async Task InvalidateFullyBlockedSessionsAsync(IEnumerable<Guid> affectedUserIds)
	{
		foreach (var userId in affectedUserIds.Distinct())
		{
			var user = await identities.FindAsync(x => x.Id == userId && x.TenantId == CurrentTenant.Id);
			if (user == null || !await IsPortalUserAsync(user) || await HasAvailableAssociationAsync(user)) continue;

			user.SetProperty(SessionRevisionProperty, Guid.NewGuid().ToString("N"));
			await identities.UpdateAsync(user);
		}
	}

	public async Task<bool> IsCurrentSessionAsync(Guid userId, string? issuedRevision)
	{
		if (!CurrentTenant.Id.HasValue) return true;
		var user = await identities.FindAsync(x => x.Id == userId && x.TenantId == CurrentTenant.Id);
		return user == null || GetSessionRevision(user) == issuedRevision || !await IsPortalUserAsync(user);
	}

	public async Task<string?> GetSessionRevisionAsync(Guid userId)
	{
		if (!CurrentTenant.Id.HasValue) return null;
		var user = await identities.FindAsync(x => x.Id == userId && x.TenantId == CurrentTenant.Id);
		return user == null ? null : GetSessionRevision(user);
	}
}
