using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportStaff : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public TransportStaffRole Role { get; private set; }
	public string? LicenseNo { get; private set; }
	public DateTime? LicenseExpiryDate { get; private set; }
	public int? ExperienceYears { get; private set; }
	public TransportStaffStatus Status { get; private set; }
	public string? Remarks { get; private set; }

	private TransportStaff()
	{
	}

	public TransportStaff(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid employeeId,
		TransportStaffRole role,
		string? licenseNo,
		DateTime? licenseExpiryDate,
		int? experienceYears,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		Status = TransportStaffStatus.Active;
		UpdateDetails(employeeId, role, licenseNo, licenseExpiryDate, experienceYears, remarks);
	}

	public void UpdateDetails(
		Guid employeeId,
		TransportStaffRole role,
		string? licenseNo,
		DateTime? licenseExpiryDate,
		int? experienceYears,
		string? remarks)
	{
		EmployeeId = employeeId;
		Role = role;
		LicenseNo = NormalizeNullable(licenseNo)?.ToUpperInvariant();
		LicenseExpiryDate = licenseExpiryDate;
		ExperienceYears = experienceYears;
		Remarks = NormalizeNullable(remarks);
	}

	public void ChangeStatus(TransportStaffStatus status, string? remarks)
	{
		Status = status;
		Remarks = NormalizeNullable(remarks) ?? Remarks;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
