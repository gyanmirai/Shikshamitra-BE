using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>
/// Tenant-owned campus/institute location used to scope admission offerings.
/// Academic boards, mediums, stages, and standards remain owned by the Academic module.
/// </summary>
public class AdmissionCampus : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public bool IsActive { get; private set; }

	protected AdmissionCampus()
	{
		IsDeleted = false;
	}

	public AdmissionCampus(Guid id, Guid? tenantId, string code, string name)
		: base(id)
	{
		TenantId = tenantId;
		IsDeleted = false;
		UpdateDetails(code, name);
		IsActive = true;
	}

	public void UpdateDetails(string code, string name)
	{
		Code = NormalizeCode(code);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private static string NormalizeCode(string code) =>
		Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
}
