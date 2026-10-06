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
		var guardianLinks = await links.GetListAsync(link => link.UserId == user.Id && link.AccountType == "guardian");
		if (!guardianLinks.Any(link => link.VerifiedByUserId != Guid.Empty)) return false;
		if (user.EmailConfirmed) return true;
		// An arbitrary unconfirmed email match is insufficient. Only this exact current
		// guardian identity/link's durable initial-setup request supplies provenance.
		foreach (var link in guardianLinks.Where(link => link.VerifiedByUserId != Guid.Empty))
			if (await requests.AnyAsync(row => row.UserId == user.Id && row.LinkId == link.Id && row.StudentId == link.StudentId &&
				row.AccountType == "guardian" && row.Email == user.Email &&
				(row.Status == "Pending" || row.Status == "Processing" || row.Status == "Sent" || row.Status == "Failed"))) return true;
		return false;
	}
}
