using System;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentCertificateTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public StudentCertificateType CertificateType { get; private set; }
	public string HeaderContent { get; private set; } = string.Empty;
	public string FooterContent { get; private set; } = string.Empty;
	public string? LogoUrl { get; private set; }
	public string? SchoolAddress { get; private set; }
	public string? PrincipalSignatureUrl { get; private set; }
	public string? SchoolStampUrl { get; private set; }
	public bool IsActive { get; private set; }

	private StudentCertificateTemplate()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentCertificateTemplate(
		Guid id,
		Guid? tenantId,
		StudentCertificateType certificateType,
		string headerContent,
		string footerContent,
		string? logoUrl,
		string? schoolAddress,
		string? principalSignatureUrl,
		string? schoolStampUrl,
		bool isActive = true)
		: base(id)
	{
		TenantId = tenantId;
		CertificateType = certificateType;
		Update(headerContent, footerContent, logoUrl, schoolAddress, principalSignatureUrl, schoolStampUrl, isActive);
	}

	public void Update(
		string headerContent,
		string footerContent,
		string? logoUrl,
		string? schoolAddress,
		string? principalSignatureUrl,
		string? schoolStampUrl,
		bool isActive)
	{
		HeaderContent = string.IsNullOrWhiteSpace(headerContent) ? DefaultHeader(CertificateType) : headerContent.Trim();
		FooterContent = string.IsNullOrWhiteSpace(footerContent) ? DefaultFooter() : footerContent.Trim();
		LogoUrl = NormalizeOptional(logoUrl);
		SchoolAddress = NormalizeOptional(schoolAddress);
		PrincipalSignatureUrl = NormalizeOptional(principalSignatureUrl);
		SchoolStampUrl = NormalizeOptional(schoolStampUrl);
		IsActive = isActive;
	}

	public static string DefaultHeader(StudentCertificateType certificateType) =>
		$"{{{{OrganizationName}}}}\n{GetDisplayName(certificateType)}\nCertificate No: {{{{CertificateNo}}}}\nIssue Date: {{{{IssueDate}}}}";

	public static string DefaultFooter() =>
		"Reason: {{Reason}}\nPrincipal Signature";

	public static string GetDisplayName(StudentCertificateType certificateType) => certificateType switch
	{
		StudentCertificateType.Bonafide => "Bonafide Certificate",
		StudentCertificateType.Character => "Character Certificate",
		StudentCertificateType.Leaving => "Leaving Certificate",
		StudentCertificateType.FirstTrial => "First Trial Certificate",
		_ => certificateType.ToString()
	};

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
