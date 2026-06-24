using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportVehicle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string RegistrationNo { get; private set; } = string.Empty;
	public string VehicleNo { get; private set; } = string.Empty;
	public string? VehicleName { get; private set; }
	public Guid VehicleTypeId { get; private set; }
	public string VehicleCompany { get; private set; } = string.Empty;
	public string Model { get; private set; } = string.Empty;
	public string YearOfManufacture { get; private set; } = string.Empty;
	public int SeatingCapacity { get; private set; }
	public string VehicleColor { get; private set; } = string.Empty;
	public string? SchoolAssignedNo { get; private set; }
	public string Owner { get; private set; } = string.Empty;
	public Guid FuelTypeId { get; private set; }
	public bool IsAc { get; private set; }
	public string? Description { get; private set; }
	public TransportVehicleStatus Status { get; private set; }

	private TransportVehicle()
	{
	}

	public TransportVehicle(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string registrationNo,
		string vehicleNo,
		string? vehicleName,
		Guid vehicleTypeId,
		string vehicleCompany,
		string model,
		string yearOfManufacture,
		int seatingCapacity,
		string vehicleColor,
		string? schoolAssignedNo,
		string owner,
		Guid fuelTypeId,
		bool isAc,
		string? description) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		Status = TransportVehicleStatus.Active;
		UpdateDetails(
			registrationNo,
			vehicleNo,
			vehicleName,
			vehicleTypeId,
			vehicleCompany,
			model,
			yearOfManufacture,
			seatingCapacity,
			vehicleColor,
			schoolAssignedNo,
			owner,
			fuelTypeId,
			isAc,
			description);
	}

	public void UpdateDetails(
		string registrationNo,
		string vehicleNo,
		string? vehicleName,
		Guid vehicleTypeId,
		string vehicleCompany,
		string model,
		string yearOfManufacture,
		int seatingCapacity,
		string vehicleColor,
		string? schoolAssignedNo,
		string owner,
		Guid fuelTypeId,
		bool isAc,
		string? description)
	{
		RegistrationNo = registrationNo.Trim().ToUpperInvariant();
		VehicleNo = vehicleNo.Trim().ToUpperInvariant();
		VehicleName = NormalizeNullable(vehicleName);
		VehicleTypeId = vehicleTypeId;
		VehicleCompany = vehicleCompany.Trim();
		Model = model.Trim();
		YearOfManufacture = yearOfManufacture.Trim();
		SeatingCapacity = seatingCapacity;
		VehicleColor = vehicleColor.Trim();
		SchoolAssignedNo = NormalizeNullable(schoolAssignedNo);
		Owner = owner.Trim();
		FuelTypeId = fuelTypeId;
		IsAc = isAc;
		Description = NormalizeNullable(description);
	}

	public void ChangeStatus(TransportVehicleStatus status)
	{
		Status = status;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
