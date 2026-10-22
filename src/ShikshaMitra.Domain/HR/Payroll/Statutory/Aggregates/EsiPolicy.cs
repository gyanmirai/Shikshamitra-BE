using System;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Code { get; private set; } = string.Empty;
	public bool IsArchived { get; private set; }
	public bool IsDefault { get; private set; }
	private EsiPolicy() { }
	public EsiPolicy(Guid id, Guid? tenantId, string name, string code, bool isDefault = false) : base(id)
	{
		TenantId = tenantId;
		if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160 || string.IsNullOrWhiteSpace(code) || code.Trim().Length > 60)
			throw new BusinessException(HRErrorCodes.EsiPolicyInvalid);
		Name = name.Trim();
		Code = code.Trim().ToUpperInvariant();
		if (!Regex.IsMatch(Code, "^[A-Z][A-Z0-9_]*$")) throw new BusinessException(HRErrorCodes.EsiPolicyInvalid);
		IsDefault = isDefault;
	}
	public void Archive()
	{
		if (IsDefault) throw new BusinessException(HRErrorCodes.EsiDefaultPolicyRequired);
		IsArchived = true;
	}
}
