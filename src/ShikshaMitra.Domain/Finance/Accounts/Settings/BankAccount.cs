using ShikshaMitra.Finance.Accounts.Settings;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

public class BankAccount : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string? Code { get; private set; }
	public string? NormalizedCode { get; private set; }
	public Guid? AccountHeadId { get; private set; }
	public string BankName { get; private set; } = string.Empty;
	public string BranchName { get; private set; } = string.Empty;
	public string? BranchAddress { get; private set; }
	public string AccountHolderName { get; private set; } = string.Empty;
	public BankAccountType AccountType { get; private set; }
	[DisableAuditing]
	public string AccountNumber { get; private set; } = string.Empty;
	public string? Purpose { get; private set; }
	[DisableAuditing]
	public string IfscCode { get; private set; } = string.Empty;
	[DisableAuditing]
	public string? UpiId { get; private set; }
	public DateTimeOffset? EffectiveFrom { get; private set; }
	public DateTimeOffset? EffectiveTo { get; private set; }
	public bool IsActive { get; private set; }
	public string? DeactivationReason { get; private set; }
	public int Version { get; private set; }

	protected BankAccount() { }

	public BankAccount(Guid id, Guid academicYearId, string bankName, string branchName,
		string? branchAddress, string accountHolderName, BankAccountType accountType,
		string accountNumber, string? purpose, string ifscCode, string? upiId,
		bool isActive = true, Guid? tenantId = null, string? code = null,
		Guid? accountHeadId = null, DateTimeOffset? effectiveFrom = null,
		DateTimeOffset? effectiveTo = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 1;
		SetDetails(code, accountHeadId, bankName, branchName, branchAddress, accountHolderName,
			accountType, accountNumber, purpose, ifscCode, upiId, effectiveFrom, effectiveTo);
		IsActive = isActive;
	}

	public void Update(string? code, Guid? accountHeadId, string bankName, string branchName,
		string? branchAddress, string accountHolderName, BankAccountType accountType,
		string accountNumber, string? purpose, string ifscCode, string? upiId,
		DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
	{
		SetDetails(code, accountHeadId, bankName, branchName, branchAddress, accountHolderName,
			accountType, accountNumber, purpose, ifscCode, upiId, effectiveFrom, effectiveTo);
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

	private void SetDetails(string? code, Guid? accountHeadId, string bankName, string branchName,
		string? branchAddress, string accountHolderName, BankAccountType accountType,
		string accountNumber, string? purpose, string ifscCode, string? upiId,
		DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
	{
		if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
			throw new BusinessException(AccountSettingErrorCodes.InvalidEffectivePeriod);
		Code = string.IsNullOrWhiteSpace(code) ? null : AccountSettingConsts.NormalizeCode(code);
		NormalizedCode = Code;
		AccountHeadId = accountHeadId;
		BankName = Check.NotNullOrWhiteSpace(bankName, nameof(bankName), AccountSettingConsts.MaxNameLength).Trim();
		BranchName = Check.NotNullOrWhiteSpace(branchName, nameof(branchName), AccountSettingConsts.MaxNameLength).Trim();
		BranchAddress = string.IsNullOrWhiteSpace(branchAddress) ? null : branchAddress.Trim();
		AccountHolderName = Check.NotNullOrWhiteSpace(accountHolderName, nameof(accountHolderName), AccountSettingConsts.MaxNameLength).Trim();
		AccountType = accountType;
		AccountNumber = Check.NotNullOrWhiteSpace(accountNumber, nameof(accountNumber), 34).Trim();
		Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim();
		IfscCode = Check.NotNullOrWhiteSpace(ifscCode, nameof(ifscCode), 11).Trim().ToUpperInvariant();
		UpiId = string.IsNullOrWhiteSpace(upiId) ? null : upiId.Trim().ToLowerInvariant();
		EffectiveFrom = effectiveFrom;
		EffectiveTo = effectiveTo;
	}
}
