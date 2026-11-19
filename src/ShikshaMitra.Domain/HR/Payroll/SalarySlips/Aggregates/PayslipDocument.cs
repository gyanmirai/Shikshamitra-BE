using System;
using System.Security.Cryptography;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>Issued employee document. Its facts and artifact are append-only.</summary>
public class PayslipDocument : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SalarySlipId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public int Revision { get; private set; }
	public DateTime IssuedAt { get; private set; }
	public Guid? IssuedBy { get; private set; }
	public string FactsJson { get; private set; } = string.Empty;
	private byte[] _pdf = [];
	public byte[] Pdf => (byte[])_pdf.Clone();
	public string Sha256 { get; private set; } = string.Empty;
	public string? ReplacementReason { get; private set; }
	public Guid? ReplacesDocumentId { get; private set; }
	public Guid? RequestId { get; private set; }

	private PayslipDocument() { }

	public PayslipDocument(Guid id, Guid? tenantId, Guid salarySlipId, Guid employeeId, string monthYear,
		int revision, DateTime issuedAt, Guid? issuedBy, string factsJson, byte[] pdf,
		Guid? replacesDocumentId = null, string? replacementReason = null, Guid? requestId = null) : base(id)
	{
		Check.NotNullOrWhiteSpace(factsJson, nameof(factsJson));
		Check.NotNull(pdf, nameof(pdf));
		if (pdf.Length == 0 || revision < 1 || salarySlipId == Guid.Empty || employeeId == Guid.Empty ||
			!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _) ||
			(revision == 1 && replacesDocumentId.HasValue) || (revision > 1 && !replacesDocumentId.HasValue) ||
			(replacesDocumentId.HasValue && (string.IsNullOrWhiteSpace(replacementReason) || !requestId.HasValue || requestId == Guid.Empty)))
			throw new BusinessException(HRErrorCodes.PayslipDocumentInvalid);
		TenantId = tenantId;
		SalarySlipId = salarySlipId;
		EmployeeId = employeeId;
		MonthYear = monthYear;
		Revision = revision;
		IssuedAt = issuedAt;
		IssuedBy = issuedBy;
		FactsJson = factsJson;
		_pdf = (byte[])pdf.Clone();
		Sha256 = Convert.ToHexString(SHA256.HashData(_pdf));
		ReplacesDocumentId = replacesDocumentId;
		ReplacementReason = replacementReason?.Trim();
		RequestId = requestId;
	}
}
