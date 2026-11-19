using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportFeeConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public TransportFeeModelType ModelType { get; private set; }
	public TransportFeeFrequency FeeFrequency { get; private set; }
	public decimal? RatePerKm { get; private set; }
	public Guid ChargeCategoryId { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public bool IsActive { get; private set; }
	public string? Remarks { get; private set; }

	private TransportFeeConfiguration()
	{
	}

	public TransportFeeConfiguration(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		TransportFeeModelType modelType,
		TransportFeeFrequency feeFrequency,
		decimal? ratePerKm,
		Guid chargeCategoryId,
		DateTime effectiveFrom,
		bool isActive,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		UpdateDetails(modelType, feeFrequency, ratePerKm, chargeCategoryId, effectiveFrom, isActive, remarks);
	}

	public void UpdateDetails(
		TransportFeeModelType modelType,
		TransportFeeFrequency feeFrequency,
		decimal? ratePerKm,
		Guid chargeCategoryId,
		DateTime effectiveFrom,
		bool isActive,
		string? remarks)
	{
		ModelType = modelType;
		FeeFrequency = feeFrequency;
		RatePerKm = ratePerKm;
		ChargeCategoryId = chargeCategoryId;
		EffectiveFrom = effectiveFrom.Date;
		IsActive = isActive;
		Remarks = NormalizeNullable(remarks);
		Validate();
	}

	private void Validate()
	{
		if (ChargeCategoryId == Guid.Empty)
		{
			throw new BusinessException(TransportErrorCodes.FeeConfigurationInvalid)
				.WithData("ChargeCategoryId", ChargeCategoryId);
		}

		if (ModelType == TransportFeeModelType.DistanceBased && (!RatePerKm.HasValue || RatePerKm.Value <= 0))
		{
			throw new BusinessException(TransportErrorCodes.FeeConfigurationInvalid)
				.WithData("RatePerKm", RatePerKm!);
		}

		if (ModelType == TransportFeeModelType.StopBased)
		{
			RatePerKm = null;
		}
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
