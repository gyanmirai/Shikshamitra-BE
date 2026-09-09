using System;
using ShikshaMitra.Finance.Accounts.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

public class AccountHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string NormalizedName { get; private set; } = string.Empty;
	public AccountHeadType HeadType { get; private set; }
	public Guid? ParentAccountHeadId { get; private set; }
	public string? Description { get; private set; }
	public bool IsActive { get; private set; }
	public string? DeactivationReason { get; private set; }
	public int Version { get; private set; }

	protected AccountHead() { }

	public AccountHead(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		AccountHeadType headType,
		Guid? parentAccountHeadId,
		string? description,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 1;
		SetDetails(name, headType, parentAccountHeadId, description);
		IsActive = isActive;
	}

	public void Update(
		string name,
		AccountHeadType headType,
		Guid? parentAccountHeadId,
		string? description)
	{
		SetDetails(name, headType, parentAccountHeadId, description);
		Version++;
	}

	public void Activate()
	{
		IsActive = true;
		DeactivationReason = null;
		Version++;
	}

	public void Deactivate(string reason)
	{
		var normalizedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedReason) ||
			normalizedReason.Length > AccountSettingConsts.MaxDeactivationReasonLength)
		{
			throw new BusinessException(AccountSettingErrorCodes.DeactivationReasonRequired);
		}

		IsActive = false;
		DeactivationReason = normalizedReason;
		Version++;
	}

	private void SetDetails(
		string name,
		AccountHeadType headType,
		Guid? parentAccountHeadId,
		string? description)
	{
		if (!Enum.IsDefined(headType))
			throw new BusinessException(AccountSettingErrorCodes.InvalidAccountHeadType);
		if (parentAccountHeadId == Id)
			throw new BusinessException(AccountSettingErrorCodes.ParentAccountHeadCycle);

		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AccountSettingConsts.MaxNameLength).Trim();
		NormalizedName = AccountSettingConsts.NormalizeName(Name);
		HeadType = headType;
		ParentAccountHeadId = parentAccountHeadId;
		Description = string.IsNullOrWhiteSpace(description) ? null : Check.Length(description.Trim(), nameof(description), AccountSettingConsts.MaxDescriptionLength);
	}
}
