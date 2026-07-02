using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public class TransportPagedRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public string? Search { get; set; }
	public TransportRecordStatus? Status { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportStopListRequestDto : TransportPagedRequestDto
{
	public string? ZoneId { get; set; }
}

public class TransportRouteListRequestDto : TransportPagedRequestDto
{
}

public class TransportStaffListRequestDto : TransportPagedRequestDto
{
	public TransportStaffRole? Role { get; set; }
	public TransportStaffStatus? StaffStatus { get; set; }
}

public class TransportStaffOptionsRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public TransportStaffRole? Role { get; set; }
	public bool OnlyAssignable { get; set; }
}

public class TransportVehicleOptionsRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public bool OnlyAssignable { get; set; }
}

public class TransportStopDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string StopName { get; set; } = string.Empty;
	public string Landmark { get; set; } = string.Empty;
	public string? ZoneId { get; set; }
	public string? Zone { get; set; }
	public string? AddressLine1 { get; set; }
	public string? AddressLine2 { get; set; }
	public string? PinCode { get; set; }
	public decimal Amount { get; set; }
	public decimal ApproxKmFromSchool { get; set; }
	public string? Description { get; set; }
	public TransportRecordStatus Status { get; set; }
	public int AssignedStudents { get; set; }
}

public class CreateUpdateTransportStopDto
{
	public string StopName { get; set; } = string.Empty;
	public string Landmark { get; set; } = string.Empty;
	public string? ZoneId { get; set; }
	public string? Zone { get; set; }
	public decimal Amount { get; set; }
	public decimal ApproxKmFromSchool { get; set; }
	public string? PinCode { get; set; }
	public string? AddressLine1 { get; set; }
	public string? AddressLine2 { get; set; }
	public string? Description { get; set; }
}

public class TransportScheduleDto
{
	public DateTimeOffset? StartTime { get; set; }
	public DateTimeOffset? EndTime { get; set; }
}

public class TransportRouteDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string RouteType { get; set; } = string.Empty;
	public decimal Distance { get; set; }
	public string? Duration { get; set; }
	public string? Description { get; set; }
	public string StartPoint { get; set; } = string.Empty;
	public string EndPoint { get; set; } = string.Empty;
	public TransportScheduleDto? PickupSchedule { get; set; }
	public TransportScheduleDto? DropoffSchedule { get; set; }
	public TransportRecordStatus Status { get; set; }
	public int Stops { get; set; }
	public int Capacity { get; set; }
	public int Students { get; set; }
	public TransportRouteAssignmentDto? VehicleAssignment { get; set; }
	public List<TransportRouteStopDto> StopData { get; set; } = new();
}

public class CreateUpdateTransportRouteDto
{
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string RouteType { get; set; } = string.Empty;
	public decimal Distance { get; set; }
	public string? Duration { get; set; }
	public string? Description { get; set; }
	public string StartPoint { get; set; } = string.Empty;
	public string EndPoint { get; set; } = string.Empty;
	public TransportScheduleDto? PickupSchedule { get; set; }
	public TransportScheduleDto? DropoffSchedule { get; set; }
}

public class AssignTransportRouteStopsDto
{
	public List<TransportRouteStopInputDto> Stops { get; set; } = new();
}

public class TransportRouteStopInputDto
{
	public Guid StopId { get; set; }
	public DateTimeOffset? ExpectedArrivalTime { get; set; }
	public DateTimeOffset? ExpectedDepartureTime { get; set; }
	public decimal? DistanceFromPrevious { get; set; }
	public int Order { get; set; }
}

public class TransportRouteStopDto : AuditedEntityDto<Guid>
{
	public Guid StopId { get; set; }
	public string StopName { get; set; } = string.Empty;
	public int Order { get; set; }
	public DateTimeOffset? ExpectedArrivalTime { get; set; }
	public DateTimeOffset? ExpectedDepartureTime { get; set; }
	public decimal? DistanceFromPrevious { get; set; }
}

public class ChangeTransportStatusDto
{
	public TransportRecordStatus Status { get; set; }
	public string? Remarks { get; set; }
}

public class TransportStaffDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public Guid EmployeeId { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string? PhoneNo { get; set; }
	public string Role { get; set; } = string.Empty;
	public TransportStaffRole RoleId { get; set; }
	public string? LicenseNo { get; set; }
	public string? LicenceNo { get; set; }
	public DateTime? LicenseExpiryDate { get; set; }
	public DateTime? LicenceExpiryDate { get; set; }
	public string? LicenceExpiry { get; set; }
	public int? ExperienceYears { get; set; }
	public string Experience { get; set; } = string.Empty;
	public TransportStaffStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public string? AssignedRoute { get; set; }
	public Guid? AssignedRouteId { get; set; }
	public string? Remarks { get; set; }
}

public class CreateUpdateTransportStaffDto
{
	public Guid EmployeeId { get; set; }
	public TransportStaffRole Role { get; set; }
	public string? LicenseNo { get; set; }
	public string? LicenceNo { get; set; }
	public DateTime? LicenseExpiryDate { get; set; }
	public DateTime? LicenceExpiryDate { get; set; }
	public int? ExperienceYears { get; set; }
	public string? Remarks { get; set; }
}

public class ChangeTransportStaffStatusDto
{
	public TransportStaffStatus Status { get; set; }
	public string? Remarks { get; set; }
}

public class AssignTransportRouteVehicleDto
{
	public Guid VehicleId { get; set; }
	public Guid DriverStaffId { get; set; }
	public Guid? ConductorStaffId { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public string? Remarks { get; set; }
}

public class TransportRouteAssignmentDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public Guid VehicleId { get; set; }
	public string VehicleName { get; set; } = string.Empty;
	public string VehicleRegistrationNo { get; set; } = string.Empty;
	public Guid DriverStaffId { get; set; }
	public Guid DriverEmployeeId { get; set; }
	public string DriverName { get; set; } = string.Empty;
	public string DriverEmployeeCode { get; set; } = string.Empty;
	public Guid? ConductorStaffId { get; set; }
	public Guid? ConductorEmployeeId { get; set; }
	public string? ConductorName { get; set; }
	public string? ConductorEmployeeCode { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsCurrent { get; set; }
	public string? Remarks { get; set; }
}

public class TransportListResultDto<T>
{
	public long TotalCount { get; set; }
	public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

public class TransportAcademicMediumRequestDto
{
	public Guid AcademicMediumId { get; set; }
}

public class TransportVehicleListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public string? Search { get; set; }
	public TransportVehicleStatus? Status { get; set; }
	public Guid? VehicleTypeId { get; set; }
	public Guid? FuelTypeId { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportVehicleTypeDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string TypeName { get; set; } = string.Empty;
	public string? ApproxSittingRange { get; set; }
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateTransportVehicleTypeDto
{
	public string Name { get; set; } = string.Empty;
	public string? ApproxSittingRange { get; set; }
	public string? Description { get; set; }
	public bool IsActive { get; set; } = true;
}

public class TransportRequiredVehicleDocumentDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string DocumentName { get; set; } = string.Empty;
	public int MaxSizeMb { get; set; }
	public int DocumentSize { get; set; }
	public List<int> AcceptedFormats { get; set; } = new();
	public List<int> AcceptedFormat { get; set; } = new();
	public bool IsRequired { get; set; }
	public bool Required { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateTransportRequiredVehicleDocumentDto
{
	public string Name { get; set; } = string.Empty;
	public int MaxSizeMb { get; set; } = 10;
	public List<int> AcceptedFormats { get; set; } = new();
	public bool IsRequired { get; set; } = true;
	public bool IsActive { get; set; } = true;
}

public class TransportServiceTypeDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string TypeName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateTransportServiceTypeDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsActive { get; set; } = true;
}

public class TransportFuelTypeDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string TypeName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateTransportFuelTypeDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsActive { get; set; } = true;
}

public class TransportOptionDto
{
	public Guid Id { get; set; }
	public string Label { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Code { get; set; }
	public string? Description { get; set; }
}

public class TransportVehicleDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string RegistrationNo { get; set; } = string.Empty;
	public string RegNo { get; set; } = string.Empty;
	public string VehicleNo { get; set; } = string.Empty;
	public string? VehicleName { get; set; }
	public Guid VehicleTypeId { get; set; }
	public string VehicleType { get; set; } = string.Empty;
	public string VehicleCompany { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public string YearOfManufacture { get; set; } = string.Empty;
	public int SeatingCapacity { get; set; }
	public int Capacity { get; set; }
	public string VehicleColor { get; set; } = string.Empty;
	public string? SchoolAssignedNo { get; set; }
	public string? SchoolAssignedNumber { get; set; }
	public string Owner { get; set; } = string.Empty;
	public Guid FuelTypeId { get; set; }
	public string FuelType { get; set; } = string.Empty;
	public bool IsAc { get; set; }
	public bool IsNonAc { get; set; }
	public string? Description { get; set; }
	public TransportVehicleStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public string? Route { get; set; }
	public string? Driver { get; set; }
	public string? ConductorName { get; set; }
	public string? InsuranceExpiry { get; set; }
	public TransportVehicleComplianceDto? Compliance { get; set; }
}

public class CreateUpdateTransportVehicleDto
{
	public string RegistrationNo { get; set; } = string.Empty;
	public string VehicleNo { get; set; } = string.Empty;
	public string? VehicleName { get; set; }
	public Guid VehicleTypeId { get; set; }
	public string VehicleCompany { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public string YearOfManufacture { get; set; } = string.Empty;
	public int SeatingCapacity { get; set; }
	public string VehicleColor { get; set; } = string.Empty;
	public string? SchoolAssignedNo { get; set; }
	public string Owner { get; set; } = string.Empty;
	public Guid FuelTypeId { get; set; }
	public bool IsAc { get; set; }
	public string? Description { get; set; }
}

public class ChangeTransportVehicleStatusDto
{
	public TransportVehicleStatus Status { get; set; }
	public string? Remarks { get; set; }
}

public class TransportVehicleDocumentUploadDto
{
	public Guid RequiredDocumentId { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
	public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class TransportVehicleDocumentDto : AuditedEntityDto<Guid>
{
	public Guid RequiredDocumentId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? Size { get; set; }
	public int MaxSizeMb { get; set; }
	public List<int> AcceptedFormats { get; set; } = new();
	public bool IsRequired { get; set; }
}

public class TransportVehicleDocumentDownloadDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = "application/octet-stream";
	public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class TransportVehicleComplianceDto
{
	public string? InsuranceCompany { get; set; }
	public string? PolicyNo { get; set; }
	public DateTime? InsuranceIssueDate { get; set; }
	public DateTime? InsuranceExpiryDate { get; set; }
	public decimal? CoverageAmount { get; set; }
	public string? PucCertificateNo { get; set; }
	public DateTime? PucIssueDate { get; set; }
	public DateTime? PucExpiryDate { get; set; }
	public string? FitnessCertificateNo { get; set; }
	public DateTime? FitnessIssueDate { get; set; }
	public DateTime? FitnessExpiryDate { get; set; }
	public string? RoadTaxReceiptNo { get; set; }
	public DateTime? RoadTaxIssueDate { get; set; }
}

public class TransportVehicleMaintenanceListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportMaintenancePhotoDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
	public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class CreateTransportVehicleMaintenanceRecordDto
{
	public string ServiceType { get; set; } = string.Empty;
	public DateTime ServiceDate { get; set; }
	public DateTime? NextServiceDate { get; set; }
	public decimal? Cost { get; set; }
	public string? Provider { get; set; }
	public string? Notes { get; set; }
	public List<TransportMaintenancePhotoDto> Photos { get; set; } = new();
}

public class TransportVehicleMaintenanceRecordDto : AuditedEntityDto<Guid>
{
	public DateTime Date { get; set; }
	public List<string> ServiceType { get; set; } = new();
	public decimal Cost { get; set; }
	public DateTime? NextDue { get; set; }
	public string? ServiceProvider { get; set; }
	public string? Description { get; set; }
}

public class TransportVehicleHistoryRowDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public Guid VehicleId { get; set; }
	public string VehicleName { get; set; } = string.Empty;
	public string VehicleRegistrationNo { get; set; } = string.Empty;
	public Guid DriverStaffId { get; set; }
	public string DriverName { get; set; } = string.Empty;
	public Guid? ConductorStaffId { get; set; }
	public string? ConductorName { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsCurrent { get; set; }
}

public class TransportMemberCandidateListRequestDto : TransportPagedRequestDto
{
	public TransportMemberType? MemberType { get; set; }
	public bool ExcludeExisting { get; set; } = true;
}

public class TransportMemberCandidateDto
{
	public Guid SourceId { get; set; }
	public TransportMemberType MemberType { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? MemberCode { get; set; }
	public string? Role { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public string? PhoneNo { get; set; }
	public string? PhotoUrl { get; set; }
	public bool HasActiveMembership { get; set; }
	public bool HasOpenApplication { get; set; }
}

public class TransportMemberListRequestDto : TransportPagedRequestDto
{
	public TransportMemberType? MemberType { get; set; }
	public TransportMemberStatus? MemberStatus { get; set; }
	public Guid? RouteId { get; set; }
	public Guid? StopId { get; set; }
}

public class TransportMemberAssignmentListRequestDto : TransportPagedRequestDto
{
}

public class TransportMemberDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public TransportMemberType MemberType { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid SourceId { get; set; }
	public TransportMemberStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public Guid? CurrentRouteId { get; set; }
	public string? CurrentRouteName { get; set; }
	public Guid? CurrentStopId { get; set; }
	public string? CurrentStopName { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? MemberCode { get; set; }
	public string? Role { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public string? PhoneNo { get; set; }
	public string? PhotoUrl { get; set; }
	public string? Remarks { get; set; }
}

public class ChangeTransportMemberStatusDto
{
	public TransportMemberStatus Status { get; set; }
	public DateTime? EffectiveDate { get; set; }
	public string? Remarks { get; set; }
}

public class TransportMemberRouteAssignmentDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public Guid MemberId { get; set; }
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public Guid StopId { get; set; }
	public string StopName { get; set; } = string.Empty;
	public Guid? ApplicationId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsCurrent { get; set; }
	public decimal FeeAmountSnapshot { get; set; }
	public string? Remarks { get; set; }
}

public class TransportApplicationListRequestDto : TransportPagedRequestDto
{
	public TransportMemberType? MemberType { get; set; }
	public TransportApplicationStatus? ApplicationStatus { get; set; }
	public TransportApplicationType? ApplicationType { get; set; }
}

public class CreateUpdateTransportApplicationDto
{
	public TransportApplicationType ApplicationType { get; set; } = TransportApplicationType.NewEnrollment;
	public TransportMemberType MemberType { get; set; }
	public Guid? MemberId { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid? PreferredStopId { get; set; }
	public Guid? RequestedStopId { get; set; }
	public string? PickupType { get; set; }
	public string? Landmark { get; set; }
	public string? AreaLocality { get; set; }
	public string? AddressLine1 { get; set; }
	public string? Description { get; set; }
}

public class AssignTransportApplicationRouteStopDto
{
	public Guid RouteId { get; set; }
	public Guid StopId { get; set; }
}

public class ChangeTransportApplicationStatusDto
{
	public TransportApplicationStatus Status { get; set; }
	public DateTime? EffectiveDate { get; set; }
	public string? Remarks { get; set; }
}

public class TransportApplicationDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public TransportApplicationType ApplicationType { get; set; }
	public string ApplicationTypeName { get; set; } = string.Empty;
	public TransportApplicationStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public TransportMemberType MemberType { get; set; }
	public Guid? MemberId { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid SourceId { get; set; }
	public Guid? RequestedStopId { get; set; }
	public string? RequestedStopName { get; set; }
	public Guid? AssignedRouteId { get; set; }
	public string? AssignedRouteName { get; set; }
	public Guid? AssignedStopId { get; set; }
	public string? AssignedStopName { get; set; }
	public string? PickupType { get; set; }
	public string? Landmark { get; set; }
	public string? AreaLocality { get; set; }
	public string? AddressLine1 { get; set; }
	public string? Description { get; set; }
	public decimal? EstimatedFeeAmount { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? MemberCode { get; set; }
	public string? Role { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public string? PhoneNo { get; set; }
	public string? PhotoUrl { get; set; }
	public string? StatusRemarks { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public Guid? ReviewedById { get; set; }
}

public class TransportSuggestedRouteDto
{
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string StartPoint { get; set; } = string.Empty;
	public string EndPoint { get; set; } = string.Empty;
	public int Capacity { get; set; }
	public int OccupiedSeats { get; set; }
	public int RemainingSeats { get; set; }
	public string Status { get; set; } = string.Empty;
	public List<TransportRouteStopDto> Stops { get; set; } = new();
}

public class TransportZoneAreaDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string ZoneName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public class CreateUpdateTransportZoneAreaDto
{
	public string Name { get; set; } = string.Empty;
	public string? ZoneName { get; set; }
	public string? Description { get; set; }
	public bool IsActive { get; set; } = true;
}

public class CreateUpdateTransportFeeConfigurationDto
{
	public TransportFeeModelType? ModelType { get; set; }
	public string? FeeModelType { get; set; }
	public string? Model { get; set; }
	public TransportFeeFrequency? FeeFrequency { get; set; }
	public string? Frequency { get; set; }
	public decimal? RatePerKm { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public bool IsActive { get; set; } = true;
	public string? Remarks { get; set; }
}

public class TransportFeeConfigurationDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public TransportFeeModelType ModelType { get; set; }
	public string Model { get; set; } = string.Empty;
	public string FeeModelType { get; set; } = string.Empty;
	public TransportFeeFrequency FeeFrequency { get; set; }
	public string Frequency { get; set; } = string.Empty;
	public decimal? RatePerKm { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public bool IsActive { get; set; }
	public string? Remarks { get; set; }
}

public class TransportFeeRunRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? FeeConfigurationId { get; set; }
	public DateTime PeriodFrom { get; set; }
	public DateTime PeriodTo { get; set; }
	public DateTime DueDate { get; set; }
	public string? Remarks { get; set; }
}

public class TransportFeeRunListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public TransportFeeRunStatus? Status { get; set; }
	public DateTime? PeriodFrom { get; set; }
	public DateTime? PeriodTo { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportFeeDueListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public TransportMemberType? MemberType { get; set; }
	public TransportFeeDueStatus? Status { get; set; }
	public Guid? RouteId { get; set; }
	public Guid? StopId { get; set; }
	public Guid? RunId { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportMemberDueListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportFeeDueDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid FeeRunId { get; set; }
	public Guid FeeConfigurationId { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public Guid MemberId { get; set; }
	public TransportMemberType MemberType { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid AssignmentId { get; set; }
	public Guid RouteId { get; set; }
	public Guid StopId { get; set; }
	public TransportFeeModelType FeeModelType { get; set; }
	public string FeeModelTypeName { get; set; } = string.Empty;
	public TransportFeeFrequency FeeFrequency { get; set; }
	public string Frequency { get; set; } = string.Empty;
	public DateTime PeriodFrom { get; set; }
	public DateTime PeriodTo { get; set; }
	public DateTime DueDate { get; set; }
	public decimal Amount { get; set; }
	public TransportFeeDueStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public Guid? FinanceChargeRequestId { get; set; }
	public Guid? FinanceChargeLineId { get; set; }
	public DateTime? FinancePostedAt { get; set; }
	public string MemberName { get; set; } = string.Empty;
	public string? MemberCode { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string StopName { get; set; } = string.Empty;
}

public class TransportFeeRunDto : AuditedEntityDto<Guid>
{
	public Guid AcademicMediumId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid FeeConfigurationId { get; set; }
	public TransportFeeRunStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public DateTime PeriodFrom { get; set; }
	public DateTime PeriodTo { get; set; }
	public DateTime DueDate { get; set; }
	public int GeneratedDueCount { get; set; }
	public int StudentDueCount { get; set; }
	public int EmployeeDueCount { get; set; }
	public decimal TotalAmount { get; set; }
	public DateTime? FinancePostedAt { get; set; }
	public int FinanceChargeRequestCount { get; set; }
	public string? Remarks { get; set; }
	public TransportFeeConfigurationDto? Configuration { get; set; }
	public List<TransportFeeDueDto> Dues { get; set; } = new();
}

public class TransportFeeRunPreviewDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid FeeConfigurationId { get; set; }
	public DateTime PeriodFrom { get; set; }
	public DateTime PeriodTo { get; set; }
	public DateTime DueDate { get; set; }
	public int GeneratedDueCount { get; set; }
	public int StudentDueCount { get; set; }
	public int EmployeeDueCount { get; set; }
	public decimal TotalAmount { get; set; }
	public List<TransportFeeDuePreviewRowDto> Items { get; set; } = new();
}

public class TransportFeeDuePreviewRowDto
{
	public Guid MemberId { get; set; }
	public TransportMemberType MemberType { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid AssignmentId { get; set; }
	public Guid RouteId { get; set; }
	public Guid StopId { get; set; }
	public string MemberName { get; set; } = string.Empty;
	public string? MemberCode { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string StopName { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public bool AlreadyGenerated { get; set; }
}

public class TransportFeeFinancePostResultDto
{
	public TransportFeeRunDto Run { get; set; } = new();
	public int PostedStudentDueCount { get; set; }
	public int SkippedEmployeeDueCount { get; set; }
	public int ChargeRequestCount { get; set; }
}

public class TransportDashboardSummaryDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int ActiveStops { get; set; }
	public int InactiveStops { get; set; }
	public int ActiveRoutes { get; set; }
	public int InactiveRoutes { get; set; }
	public int ActiveVehicles { get; set; }
	public int InactiveVehicles { get; set; }
	public int MaintenanceVehicles { get; set; }
	public int ActiveDrivers { get; set; }
	public int ActiveConductors { get; set; }
	public int InactiveStaff { get; set; }
	public int ActiveMembers { get; set; }
	public int InactiveMembers { get; set; }
	public int StudentMembers { get; set; }
	public int EmployeeMembers { get; set; }
	public int PendingApplications { get; set; }
	public int ApprovedApplications { get; set; }
	public int HeldApplications { get; set; }
	public int RejectedApplications { get; set; }
	public int CancelledApplications { get; set; }
	public int PendingFeeDues { get; set; }
	public int PostedFeeDues { get; set; }
	public int PendingStudentDues { get; set; }
	public int PendingEmployeeDues { get; set; }
	public decimal PendingDueAmount { get; set; }
	public int AssignedRoutes { get; set; }
	public int UnassignedRoutes { get; set; }
	public int RouteCapacity { get; set; }
	public int OccupiedSeats { get; set; }
	public decimal RouteCapacityUsagePercent { get; set; }
	public int MissingRequiredDocuments { get; set; }
	public int ExpiringComplianceItems { get; set; }
	public int FeeRunsPendingFinancePost { get; set; }
	public List<TransportDashboardRouteUsageDto> RouteCapacityUsage { get; set; } = new();
	public List<TransportDashboardOperationRowDto> ActiveRouteOperations { get; set; } = new();
}

public class TransportDashboardRouteUsageDto
{
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public int Capacity { get; set; }
	public int OccupiedSeats { get; set; }
	public int RemainingSeats { get; set; }
	public decimal UsagePercent { get; set; }
	public bool HasVehicleAssignment { get; set; }
	public string? VehicleRegistrationNo { get; set; }
	public string? DriverName { get; set; }
}

public class TransportDashboardOperationRowDto
{
	public Guid RouteId { get; set; }
	public string RouteName { get; set; } = string.Empty;
	public string RouteCode { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int Stops { get; set; }
	public int Members { get; set; }
	public int Capacity { get; set; }
	public string? Vehicle { get; set; }
	public string? Driver { get; set; }
	public string? Conductor { get; set; }
}

public class TransportDashboardAlertListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public string? Category { get; set; }
	public string? Severity { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 100);
}

public class TransportDashboardAlertDto
{
	public string Id { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Severity { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public Guid? EntityId { get; set; }
	public string? EntityType { get; set; }
	public DateTime CreatedAt { get; set; }
}

public class TransportReportCatalogItemDto
{
	public string Key { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool IsSupported { get; set; }
}

public class TransportReportListRequestDto
{
	public Guid AcademicMediumId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int PageNumber { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public Guid? RouteId { get; set; }
	public Guid? StopId { get; set; }
	public Guid? VehicleId { get; set; }
	public Guid? StaffId { get; set; }
	public TransportMemberType? MemberType { get; set; }
	public string? Status { get; set; }

	public int SkipCount => (Math.Max(PageNumber, 1) - 1) * NormalizedPageSize;
	public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 10 : PageSize, 1, 200);
}

public class TransportReportResultDto
{
	public string Key { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public bool IsSupported { get; set; }
	public string? Message { get; set; }
	public long TotalCount { get; set; }
	public List<TransportReportColumnDto> Columns { get; set; } = new();
	public List<Dictionary<string, string?>> Items { get; set; } = new();
}

public class TransportReportColumnDto
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}

public class TransportReportExportDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = "text/csv";
	public byte[] Content { get; set; } = Array.Empty<byte>();
}
