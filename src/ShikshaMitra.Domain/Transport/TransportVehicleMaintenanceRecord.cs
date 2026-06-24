using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportVehicleMaintenanceRecord : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid VehicleId { get; private set; }
	public string ServiceType { get; private set; } = string.Empty;
	public DateTime ServiceDate { get; private set; }
	public DateTime? NextServiceDate { get; private set; }
	public decimal? Cost { get; private set; }
	public string? Provider { get; private set; }
	public string? Notes { get; private set; }
	public string? PhotoBlobNames { get; private set; }

	private TransportVehicleMaintenanceRecord()
	{
	}

	public TransportVehicleMaintenanceRecord(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid vehicleId,
		string serviceType,
		DateTime serviceDate,
		DateTime? nextServiceDate,
		decimal? cost,
		string? provider,
		string? notes,
		string? photoBlobNames) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		VehicleId = vehicleId;
		ServiceType = serviceType.Trim();
		ServiceDate = serviceDate;
		NextServiceDate = nextServiceDate;
		Cost = cost;
		Provider = NormalizeNullable(provider);
		Notes = NormalizeNullable(notes);
		PhotoBlobNames = NormalizeNullable(photoBlobNames);
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
