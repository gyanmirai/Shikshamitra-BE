using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportVehicleType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? ApproxSittingRange { get; private set; }
	public string? Description { get; private set; }
	public bool IsActive { get; private set; }

	private TransportVehicleType()
	{
	}

	public TransportVehicleType(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string name,
		string? approxSittingRange,
		string? description,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		UpdateDetails(name, approxSittingRange, description, isActive);
	}

	public void UpdateDetails(string name, string? approxSittingRange, string? description, bool isActive)
	{
		Name = name.Trim();
		ApproxSittingRange = NormalizeNullable(approxSittingRange);
		Description = NormalizeNullable(description);
		IsActive = isActive;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
