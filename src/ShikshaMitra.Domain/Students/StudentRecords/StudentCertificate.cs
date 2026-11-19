using System;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentCertificate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string CertificateNumber { get; private set; } = string.Empty;
	public StudentCertificateType CertificateType { get; private set; }
	public DateTimeOffset IssueDate { get; private set; }
	public Guid? GeneratedByUserId { get; private set; }
	public StudentCertificateStatus Status { get; private set; }
	public string CertificateDataJson { get; private set; } = "{}";
	public string TemplateSnapshotJson { get; private set; } = "{}";
	public string? IdempotencyKey { get; private set; }
	public Guid? TransitionId { get; private set; }
	public Guid? SourceCertificateId { get; private set; }

	private StudentCertificate()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentCertificate(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		string certificateNumber,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		Guid? generatedByUserId,
		StudentCertificateStatus status,
		string certificateDataJson,
		string templateSnapshotJson, Guid? academicYearId = null)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		AcademicYearId = academicYearId;
		CertificateNumber = Check.NotNullOrWhiteSpace(certificateNumber, nameof(certificateNumber), maxLength: 80);
		CertificateType = certificateType;
		IssueDate = issueDate;
		GeneratedByUserId = generatedByUserId;
		Status = status;
		CertificateDataJson = string.IsNullOrWhiteSpace(certificateDataJson) ? "{}" : certificateDataJson;
		TemplateSnapshotJson = string.IsNullOrWhiteSpace(templateSnapshotJson) ? "{}" : templateSnapshotJson;
	}

	public void RecordIssuanceContext(string idempotencyKey, Guid? transitionId, Guid? sourceCertificateId = null)
	{
		IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), 100).Trim();
		TransitionId = transitionId; SourceCertificateId = sourceCertificateId;
	}

	public void MarkPrinted()
	{
		Status = StudentCertificateStatus.Printed;
	}

	public void MarkIssued()
	{
		Status = StudentCertificateStatus.Issued;
	}

	public void Cancel()
	{
		Status = StudentCertificateStatus.Cancelled;
	}
}
