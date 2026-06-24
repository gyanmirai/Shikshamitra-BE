using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportRequiredVehicleDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int MaxSizeMb { get; private set; }
	public string AcceptedFormatsCsv { get; private set; } = string.Empty;
	public bool IsRequired { get; private set; }
	public bool IsActive { get; private set; }

	private TransportRequiredVehicleDocument()
	{
	}

	public TransportRequiredVehicleDocument(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string name,
		int maxSizeMb,
		string acceptedFormatsCsv,
		bool isRequired,
		bool isActive = true) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		UpdateDetails(name, maxSizeMb, acceptedFormatsCsv, isRequired, isActive);
	}

	public void UpdateDetails(
		string name,
		int maxSizeMb,
		string acceptedFormatsCsv,
		bool isRequired,
		bool isActive)
	{
		Name = name.Trim();
		MaxSizeMb = maxSizeMb;
		AcceptedFormatsCsv = acceptedFormatsCsv.Trim();
		IsRequired = isRequired;
		IsActive = isActive;
	}
}
