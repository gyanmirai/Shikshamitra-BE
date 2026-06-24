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
	public string CertificateNumber { get; private set; } = string.Empty;
	public StudentCertificateType CertificateType { get; private set; }
	public DateTimeOffset IssueDate { get; private set; }
	public Guid? GeneratedByUserId { get; private set; }
	public StudentCertificateStatus Status { get; private set; }
	public string CertificateDataJson { get; private set; } = "{}";
	public string TemplateSnapshotJson { get; private set; } = "{}";

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
		string templateSnapshotJson)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		CertificateNumber = Check.NotNullOrWhiteSpace(certificateNumber, nameof(certificateNumber), maxLength: 32);
		CertificateType = certificateType;
		IssueDate = issueDate;
		GeneratedByUserId = generatedByUserId;
		Status = status;
		CertificateDataJson = string.IsNullOrWhiteSpace(certificateDataJson) ? "{}" : certificateDataJson;
		TemplateSnapshotJson = string.IsNullOrWhiteSpace(templateSnapshotJson) ? "{}" : templateSnapshotJson;
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
