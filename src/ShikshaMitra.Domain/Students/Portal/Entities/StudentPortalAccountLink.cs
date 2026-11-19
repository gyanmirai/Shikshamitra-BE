using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Portal;

/// <summary>A staff-verified relationship, never inferred from a shared email address.</summary>
public class StudentPortalAccountLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid UserId { get; private set; }
	public string AccountType { get; private set; } = string.Empty;
	public string Relationship { get; private set; } = string.Empty;
	public Guid VerifiedByUserId { get; private set; }
	protected StudentPortalAccountLink() { }
	public StudentPortalAccountLink(Guid id, Guid tenantId, Guid studentId, Guid userId,
		string accountType, string relationship, Guid verifiedByUserId) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		UserId = Check.NotDefaultOrNull<Guid>(userId, nameof(userId));
		VerifiedByUserId = Check.NotDefaultOrNull<Guid>(verifiedByUserId, nameof(verifiedByUserId));
		if (accountType is not ("student" or "guardian")) throw new ArgumentException("Invalid account type.", nameof(accountType));
		if (accountType == "student" ? relationship != "self" : relationship is not ("father" or "mother" or "guardian"))
			throw new ArgumentException("Invalid relationship.", nameof(relationship));
		AccountType = accountType;
		Relationship = relationship;
	}
}
