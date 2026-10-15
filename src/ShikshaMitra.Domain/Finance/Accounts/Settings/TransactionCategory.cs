using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

public class TransactionCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public TransactionType? CategoryType { get; private set; }
	public string? Code { get; private set; }
	public string? NormalizedCode { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? NormalizedName { get; private set; }
	public string? Description { get; private set; }
	public Guid? DefaultAccountHeadId { get; private set; }
	public DateTimeOffset? EffectiveFrom { get; private set; }
	public DateTimeOffset? EffectiveTo { get; private set; }
	public bool IsActive { get; private set; }
	public string? DeactivationReason { get; private set; }
	public int Version { get; private set; }

	private readonly List<Tag> _tags = [];
	public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

	protected TransactionCategory() { }

	public TransactionCategory(
		Guid id, Guid? tenantId, Guid academicYearId, TransactionType categoryType,
		string name, string? description, IEnumerable<string>? tags = null,
		bool isActive = true, string? code = null, Guid? defaultAccountHeadId = null,
		DateTimeOffset? effectiveFrom = null, DateTimeOffset? effectiveTo = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		CategoryType = categoryType;
		Version = 1;
		SetDetails(code, name, description, defaultAccountHeadId, effectiveFrom, effectiveTo);
		SetTags(tags ?? []);
		IsActive = isActive;
	}

	public void Update(string? code, string name, string? description, Guid? defaultAccountHeadId,
		DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo, IEnumerable<string> tags)
	{
		SetDetails(code, name, description, defaultAccountHeadId, effectiveFrom, effectiveTo);
		SetTags(tags);
		Version++;
	}

	public void Activate() { IsActive = true; DeactivationReason = null; Version++; }

	public void Deactivate(string reason)
	{
		var normalizedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > AccountSettingConsts.MaxDeactivationReasonLength)
			throw new BusinessException(AccountSettingErrorCodes.DeactivationReasonRequired);
		IsActive = false;
		DeactivationReason = normalizedReason;
		Version++;
	}

	public void AddTag(string value)
	{
		var tag = new Tag(value);
		if (_tags.All(t => t.Value != tag.Value)) _tags.Add(tag);
	}

	public void RemoveTag(string value) => _tags.RemoveAll(t => t.Value == value.Trim().ToLowerInvariant());
	public void ClearTags() => _tags.Clear();
	public void SetTags(IEnumerable<string> tags) { _tags.Clear(); foreach (var tag in tags) AddTag(tag); }

	private void SetDetails(string? code, string name, string? description, Guid? defaultAccountHeadId,
		DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
	{
		if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
			throw new BusinessException(AccountSettingErrorCodes.InvalidEffectivePeriod);
		Code = string.IsNullOrWhiteSpace(code) ? null : AccountSettingConsts.NormalizeCode(code);
		NormalizedCode = Code;
		Name = Check.NotNullOrWhiteSpace(name?.Trim(), nameof(name),
			CategoryType == TransactionType.Income
				? AccountSettingConsts.MaxIncomeCategoryNameLength
				: AccountSettingConsts.MaxNameLength);
		NormalizedName = AccountSettingConsts.NormalizeName(Name);
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		DefaultAccountHeadId = defaultAccountHeadId;
		EffectiveFrom = effectiveFrom;
		EffectiveTo = effectiveTo;
	}
}
