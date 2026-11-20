using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSubmission : AggregateRoot<Guid>, IMultiTenant
{
	public const string Pending = "Pending";
	public const string Finalized = "Finalized";
	public const string Returned = "Returned";
	public const string Withdrawn = "Withdrawn";

	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string Month { get; private set; } = "";
	public Guid SourceRunId { get; private set; }
	public Guid InputRevision { get; private set; }
	public Guid SubmitterId { get; private set; }
	public Guid ApproverId { get; private set; }
	public bool SelfFinalization { get; private set; }
	public int SettingsVersion { get; private set; }
	public Guid? PreviousSubmissionId { get; private set; }
	public string State { get; private set; } = Pending;
	public int Version { get; private set; } = 1;
	public decimal NetPay { get; private set; }
	public int StaffCount { get; private set; }
	public DateTime SubmittedAt { get; private set; }
	public DateTime? ResolvedAt { get; private set; }
	public string? ReasonCode { get; private set; }

	private PayrollSubmission() { }

	public PayrollSubmission(Guid id, Guid tenantId, Guid? academicYearId, string month, Guid sourceRunId,
		Guid inputRevision, Guid submitterId, Guid approverId, bool selfFinalization, int settingsVersion,
		Guid? previousSubmissionId, decimal netPay, int staffCount, DateTime submittedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Month = month;
		SourceRunId = sourceRunId;
		InputRevision = inputRevision;
		SubmitterId = submitterId;
		ApproverId = approverId;
		SelfFinalization = selfFinalization;
		SettingsVersion = settingsVersion;
		PreviousSubmissionId = previousSubmissionId;
		NetPay = netPay;
		StaffCount = staffCount;
		SubmittedAt = submittedAt;
	}

	public void Resolve(int expectedVersion, string state, DateTime resolvedAt, string reasonCode)
	{
		if (Version != expectedVersion || State != Pending)
			throw new BusinessException("PAYROLL_SUBMISSION_VERSION_CONFLICT");
		if (state is not (Finalized or Returned or Withdrawn))
			throw new ArgumentOutOfRangeException(nameof(state));
		State = state;
		Version++;
		ResolvedAt = resolvedAt;
		ReasonCode = reasonCode;
	}
}

public class PayrollSubmissionEmployee : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SubmissionId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid PreviewEmployeeId { get; private set; }
	public string EvidenceDigest { get; private set; } = "";
	public decimal NetPay { get; private set; }

	private PayrollSubmissionEmployee() { }

	public PayrollSubmissionEmployee(Guid id, Guid tenantId, Guid submissionId, Guid employeeId, Guid previewEmployeeId,
		string evidenceDigest, decimal netPay) : base(id)
	{
		TenantId = tenantId;
		SubmissionId = submissionId;
		EmployeeId = employeeId;
		PreviewEmployeeId = previewEmployeeId;
		EvidenceDigest = evidenceDigest;
		NetPay = netPay;
	}
}

public class PayrollEmployeeClaim : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Month { get; private set; } = "";
	public Guid EmployeeId { get; private set; }
	public Guid? SubmissionId { get; private set; }
	public long Generation { get; private set; }

	private PayrollEmployeeClaim() { }
	public PayrollEmployeeClaim(Guid id, Guid tenantId, string month, Guid employeeId) : base(id)
	{
		TenantId = tenantId;
		Month = month;
		EmployeeId = employeeId;
	}

	public void Claim(Guid submissionId)
	{
		if (SubmissionId.HasValue) throw new BusinessException("PAYROLL_EMPLOYEE_ALREADY_SUBMITTED");
		SubmissionId = submissionId;
		Generation = checked(Generation + 1);
	}

	public void Release(Guid submissionId)
	{
		if (SubmissionId != submissionId) throw new BusinessException("PAYROLL_SUBMISSION_CLAIM_MISMATCH");
		SubmissionId = null;
	}
}
