using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportFuelType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public bool IsActive { get; private set; }

	private TransportFuelType()
	{
	}

	public TransportFuelType(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string name,
		string? description,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		UpdateDetails(name, description, isActive);
	}

	public void UpdateDetails(string name, string? description, bool isActive)
	{
		Name = name.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		IsActive = isActive;
	}
}
