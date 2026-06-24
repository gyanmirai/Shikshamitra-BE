using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportRouteAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid RouteId { get; private set; }
	public Guid VehicleId { get; private set; }
	public Guid DriverStaffId { get; private set; }
	public Guid? ConductorStaffId { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public string? Remarks { get; private set; }

	public bool IsCurrent => EffectiveTo == null;

	private TransportRouteAssignment()
	{
	}

	public TransportRouteAssignment(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid routeId,
		Guid vehicleId,
		Guid driverStaffId,
		Guid? conductorStaffId,
		DateTime effectiveFrom,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		RouteId = routeId;
		VehicleId = vehicleId;
		DriverStaffId = driverStaffId;
		ConductorStaffId = conductorStaffId;
		EffectiveFrom = effectiveFrom;
		Remarks = NormalizeNullable(remarks);
	}

	public void Close(DateTime effectiveTo, string? remarks)
	{
		EffectiveTo = effectiveTo;
		Remarks = NormalizeNullable(remarks) ?? Remarks;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
