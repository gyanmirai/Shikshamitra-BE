using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportVehicleCompliance : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid VehicleId { get; private set; }
	public string? InsuranceCompany { get; private set; }
	public string? PolicyNo { get; private set; }
	public DateTime? InsuranceIssueDate { get; private set; }
	public DateTime? InsuranceExpiryDate { get; private set; }
	public decimal? CoverageAmount { get; private set; }
	public string? PucCertificateNo { get; private set; }
	public DateTime? PucIssueDate { get; private set; }
	public DateTime? PucExpiryDate { get; private set; }
	public string? FitnessCertificateNo { get; private set; }
	public DateTime? FitnessIssueDate { get; private set; }
	public DateTime? FitnessExpiryDate { get; private set; }
	public string? RoadTaxReceiptNo { get; private set; }
	public DateTime? RoadTaxIssueDate { get; private set; }

	private TransportVehicleCompliance()
	{
	}

	public TransportVehicleCompliance(Guid id, Guid? tenantId, Guid academicMediumId, Guid vehicleId) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		VehicleId = vehicleId;
	}

	public void UpdateDetails(
		string? insuranceCompany,
		string? policyNo,
		DateTime? insuranceIssueDate,
		DateTime? insuranceExpiryDate,
		decimal? coverageAmount,
		string? pucCertificateNo,
		DateTime? pucIssueDate,
		DateTime? pucExpiryDate,
		string? fitnessCertificateNo,
		DateTime? fitnessIssueDate,
		DateTime? fitnessExpiryDate,
		string? roadTaxReceiptNo,
		DateTime? roadTaxIssueDate)
	{
		InsuranceCompany = NormalizeNullable(insuranceCompany);
		PolicyNo = NormalizeNullable(policyNo);
		InsuranceIssueDate = insuranceIssueDate;
		InsuranceExpiryDate = insuranceExpiryDate;
		CoverageAmount = coverageAmount;
		PucCertificateNo = NormalizeNullable(pucCertificateNo);
		PucIssueDate = pucIssueDate;
		PucExpiryDate = pucExpiryDate;
		FitnessCertificateNo = NormalizeNullable(fitnessCertificateNo);
		FitnessIssueDate = fitnessIssueDate;
		FitnessExpiryDate = fitnessExpiryDate;
		RoadTaxReceiptNo = NormalizeNullable(roadTaxReceiptNo);
		RoadTaxIssueDate = roadTaxIssueDate;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
