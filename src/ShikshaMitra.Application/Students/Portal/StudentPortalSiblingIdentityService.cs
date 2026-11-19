using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

/// <summary>Reuse an existing verified parent, including its own known pending initial email setup.</summary>
public class StudentPortalSiblingIdentityService(
	IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<StudentCredentialEmailRequest, Guid> requests,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<bool> HasVerifiedGuardianIdentityAsync(IdentityUser user)
	{
		if (!tenant.Id.HasValue || user.TenantId != tenant.Id || !user.IsActive) return false;
		if (user.EmailConfirmed)
			return await links.AnyAsync(link => link.UserId == user.Id && link.AccountType == "guardian" && link.VerifiedByUserId != Guid.Empty);
		// An arbitrary unconfirmed email match is insufficient. Only this exact current
		// guardian identity/link's durable initial-setup request supplies provenance.
		var requestQuery = await requests.GetQueryableAsync();
		return await links.AnyAsync(link => link.UserId == user.Id && link.AccountType == "guardian" && link.VerifiedByUserId != Guid.Empty &&
			requestQuery.Any(row => row.UserId == user.Id && row.LinkId == link.Id && row.StudentId == link.StudentId &&
				row.AccountType == "guardian" && row.Email == user.Email &&
				(row.Status == "Pending" || row.Status == "Processing" || row.Status == "Sent" || row.Status == "Failed")));
	}
}
