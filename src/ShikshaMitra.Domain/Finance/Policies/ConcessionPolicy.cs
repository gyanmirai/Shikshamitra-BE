using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public string PolicyName { get; protected set; } = string.Empty;
	public string PolicyCode { get; protected set; } = string.Empty;
	public string Description { get; protected set; } = string.Empty;
	public ConcessionPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public ConcessionValueSource ValueSource { get; protected set; }
	public ConcessionCalculationMethod CalculationMethod { get; protected set; }
	public decimal? PercentageValue { get; protected set; }
	public decimal? FixedAmount { get; protected set; }
	public decimal? MaximumPercentage { get; protected set; }
	public decimal? MaximumFixedAmount { get; protected set; }
	public ConcessionApplyOn ApplyOn { get; protected set; }
	public RoundingMethodType RoundingMethod { get; protected set; }
	public int Version { get; protected set; }
	public string ApplicableConcessionTypeIdsJson { get; protected set; } = "[]";
	public ConcessionFeeHeadScope FeeHeadScope { get; protected set; }
	public string ApplicableFeeHeadIdsJson { get; protected set; } = "[]";
	public IReadOnlyList<Guid> ApplicableConcessionTypeIds => ReadIds(ApplicableConcessionTypeIdsJson);
	public IReadOnlyList<Guid> ApplicableFeeHeadIds => ReadIds(ApplicableFeeHeadIdsJson);

	protected ConcessionPolicy() { }

	public ConcessionPolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		ConcessionPolicyUpdateModel model) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 0;
		Update(model);
	}

	public void Update(ConcessionPolicyUpdateModel model)
	{
		if (IsDefault && (!model.IsDefault || model.Status != ConcessionPolicyStatus.Active))
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		if (Status == ConcessionPolicyStatus.Archived && Version > 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (AcademicYearId == Guid.Empty)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyName))
			throw new BusinessException(ConcessionPolicyErrorCodes.NameRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyCode))
			throw new BusinessException(ConcessionPolicyErrorCodes.CodeRequired);
		if (model.Status == ConcessionPolicyStatus.Archived)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidStatus);
		if (model.IsDefault && model.Status != ConcessionPolicyStatus.Active)
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyMustBeActive);

		ValidateRules(model);

		PolicyName = model.PolicyName.Trim();
		PolicyCode = model.PolicyCode.Trim().ToUpperInvariant();
		Description = model.Description?.Trim() ?? string.Empty;
		Status = model.Status;
		IsDefault = model.IsDefault;
		ValueSource = model.ValueSource;
		CalculationMethod = model.CalculationMethod;
		PercentageValue = model.CalculationMethod == ConcessionCalculationMethod.Percentage
			? model.PercentageValue : null;
		FixedAmount = model.CalculationMethod == ConcessionCalculationMethod.FixedAmount
			? model.FixedAmount : null;
		MaximumPercentage = model.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits &&
			model.CalculationMethod == ConcessionCalculationMethod.Percentage ? model.MaximumPercentage : null;
		MaximumFixedAmount = model.ValueSource == ConcessionValueSource.RequestEnteredWithinLimits &&
			model.CalculationMethod == ConcessionCalculationMethod.FixedAmount ? model.MaximumFixedAmount : null;
		ApplyOn = model.ApplyOn;
		RoundingMethod = model.RoundingMethod;
		ApplicableConcessionTypeIdsJson = JsonSerializer.Serialize(NormalizeIds(model.ApplicableConcessionTypeIds));
		FeeHeadScope = model.FeeHeadScope;
		ApplicableFeeHeadIdsJson = JsonSerializer.Serialize(model.FeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads
			? NormalizeIds(model.ApplicableFeeHeadIds) : []);
		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
		Status = ConcessionPolicyStatus.Archived;
		IsDefault = false;
		Version++;
	}

	public void Unarchive()
	{
		if (Status != ConcessionPolicyStatus.Archived) return;
		Status = ConcessionPolicyStatus.Draft;
		IsDefault = false;
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != ConcessionPolicyStatus.Active)
			throw new BusinessException(ConcessionPolicyErrorCodes.DefaultPolicyMustBeActive);
		if (IsDefault == isDefault) return;
		IsDefault = isDefault;
		Version++;
	}

	public ConcessionPolicySnapshot ToSnapshot() => new(
		Id,
		AcademicYearId,
		PolicyName,
		PolicyCode,
		Status,
		IsDefault,
		ValueSource,
		CalculationMethod,
		PercentageValue,
		FixedAmount,
		MaximumPercentage,
		MaximumFixedAmount,
		ApplyOn,
		RoundingMethod,
		Version,
		ApplicableConcessionTypeIds.ToList(),
		FeeHeadScope,
		ApplicableFeeHeadIds.ToList());

	private static List<Guid> NormalizeIds(IReadOnlyList<Guid>? ids) =>
		(ids ?? []).Where(id => id != Guid.Empty).Distinct().OrderBy(id => id).ToList();

	private static List<Guid> ReadIds(string? json) =>
		string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Guid>>(json) ?? [];

	private static void ValidateRules(ConcessionPolicyUpdateModel model)
	{
		if (model.ValueSource == ConcessionValueSource.PolicyFixed)
		{
			if (model.CalculationMethod == ConcessionCalculationMethod.Percentage &&
				(model.PercentageValue is null or <= 0 or > 100))
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
			if (model.CalculationMethod == ConcessionCalculationMethod.FixedAmount &&
				(model.FixedAmount is null or <= 0))
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		}
		else
		{
			if (model.CalculationMethod == ConcessionCalculationMethod.FullWaiver)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
			if (model.CalculationMethod == ConcessionCalculationMethod.Percentage &&
				model.MaximumPercentage is null or <= 0 or > 100)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
			if (model.CalculationMethod == ConcessionCalculationMethod.FixedAmount &&
				model.MaximumFixedAmount is null or <= 0)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		}

		if (model.MaximumPercentage is < 0 or > 100 || model.MaximumFixedAmount is < 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
	}
}

public sealed record ConcessionPolicyUpdateModel(
	string PolicyName,
	string PolicyCode,
	string? Description,
	ConcessionPolicyStatus Status,
	bool IsDefault,
	ConcessionValueSource ValueSource,
	ConcessionCalculationMethod CalculationMethod,
	decimal? PercentageValue,
	decimal? FixedAmount,
	decimal? MaximumPercentage,
	decimal? MaximumFixedAmount,
	ConcessionApplyOn ApplyOn,
	RoundingMethodType RoundingMethod,
	IReadOnlyList<Guid>? ApplicableConcessionTypeIds = null,
	ConcessionFeeHeadScope FeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure,
	IReadOnlyList<Guid>? ApplicableFeeHeadIds = null);

public sealed record ConcessionPolicySnapshot(
	Guid PolicyId,
	Guid AcademicYearId,
	string PolicyName,
	string PolicyCode,
	ConcessionPolicyStatus Status,
	bool IsDefault,
	ConcessionValueSource ValueSource,
	ConcessionCalculationMethod CalculationMethod,
	decimal? PercentageValue,
	decimal? FixedAmount,
	decimal? MaximumPercentage,
	decimal? MaximumFixedAmount,
	ConcessionApplyOn ApplyOn,
	RoundingMethodType RoundingMethod,
	int Version,
	List<Guid>? ApplicableConcessionTypeIds = null,
	ConcessionFeeHeadScope FeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure,
	List<Guid>? ApplicableFeeHeadIds = null);
