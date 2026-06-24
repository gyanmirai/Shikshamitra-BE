using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportStop : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string StopName { get; private set; } = string.Empty;
	public string Landmark { get; private set; } = string.Empty;
	public string? ZoneId { get; private set; }
	public string? AddressLine1 { get; private set; }
	public string? AddressLine2 { get; private set; }
	public string? PinCode { get; private set; }
	public decimal Amount { get; private set; }
	public decimal ApproxKmFromSchool { get; private set; }
	public string? Description { get; private set; }
	public TransportRecordStatus Status { get; private set; }

	private TransportStop()
	{
	}

	public TransportStop(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string stopName,
		string landmark,
		string? zoneId,
		decimal amount,
		decimal approxKmFromSchool,
		string? pinCode,
		string? addressLine1,
		string? addressLine2,
		string? description) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		Status = TransportRecordStatus.Active;
		UpdateDetails(stopName, landmark, zoneId, amount, approxKmFromSchool, pinCode, addressLine1, addressLine2, description);
	}

	public void UpdateDetails(
		string stopName,
		string landmark,
		string? zoneId,
		decimal amount,
		decimal approxKmFromSchool,
		string? pinCode,
		string? addressLine1,
		string? addressLine2,
		string? description)
	{
		StopName = stopName.Trim();
		Landmark = landmark.Trim();
		ZoneId = NormalizeNullable(zoneId);
		Amount = amount;
		ApproxKmFromSchool = approxKmFromSchool;
		PinCode = NormalizeNullable(pinCode);
		AddressLine1 = NormalizeNullable(addressLine1);
		AddressLine2 = NormalizeNullable(addressLine2);
		Description = NormalizeNullable(description);
	}

	public void ChangeStatus(TransportRecordStatus status)
	{
		Status = status;
	}

	private static string? NormalizeNullable(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
