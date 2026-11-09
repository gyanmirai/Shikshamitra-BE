using System;
using Volo.Abp;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollPreviewRun : AggregateRoot<Guid>, IMultiTenant
{
	public const int CurrentCalculationVersion = 1;
	public const string CalculationVersionProperty = "CalculationVersion";
	[NotMapped]
	public int CalculationVersion => this.GetProperty<int>(CalculationVersionProperty, 0);
	[NotMapped]
	public bool HasCurrentCalculationVersion => CalculationVersion == CurrentCalculationVersion;
	public Guid? TenantId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public string Month { get; private set; } = "";
	public Guid? AcademicYearId { get; private set; }
	public string ScopeFingerprint { get; private set; } = "";
	public string Status { get; private set; } = "QUEUED";
	public Guid InputRevision { get; private set; }
	public DateTime CreatedAtUtc { get; private set; }
	public DateTime? StartedAtUtc { get; private set; }
	public DateTime? CompletedAtUtc { get; private set; }
	public string? SummaryJson { get; private set; }
	public string? ErrorCode { get; private set; }
	public int EmployeeCount { get; private set; }
	public int Attempts { get; private set; }
	public int CompletedEmployees { get; private set; }
	public bool IsTerminal => Status is "SUCCEEDED" or "FAILED" or "SUPERSEDED";
	private PayrollPreviewRun() { }
	public PayrollPreviewRun(Guid id, Guid? tenantId, Guid requestedBy, string month, Guid? academicYearId,
		string scopeFingerprint, int employeeCount, DateTime now) : base(id)
	{
		TenantId = tenantId; RequestedBy = requestedBy; Month = month; AcademicYearId = academicYearId;
		ScopeFingerprint = scopeFingerprint; EmployeeCount = employeeCount; CreatedAtUtc = now;
		this.SetProperty(CalculationVersionProperty, CurrentCalculationVersion);
	}
	public void Start(Guid revision, DateTime now)
	{
		if (IsTerminal || Attempts >= 3) throw new BusinessException(HRErrorCodes.PayrollPreviewFailed);
		Status = "RUNNING"; InputRevision = revision; StartedAtUtc = now; Attempts++; ErrorCode = null;
		CompletedEmployees = 0;
	}
	public void ReportProgress(int attempt, int completed)
	{
		EnsureOwner(attempt);
		CompletedEmployees = Math.Clamp(completed, CompletedEmployees, EmployeeCount);
	}
	public void Retry(int attempt, string code)
	{
		EnsureOwner(attempt);
		if (Attempts >= 3) throw new BusinessException(HRErrorCodes.PayrollPreviewFailed);
		Status = "QUEUED"; ErrorCode = code; CompletedEmployees = 0;
	}
	public void EnsureOwner(int attempt)
	{
		if (Status != "RUNNING" || Attempts != attempt) throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
	}
	public void Complete(string summary, DateTime now)
	{
		EnsureOwner(Attempts);
		Status = "SUCCEEDED"; SummaryJson = summary; CompletedAtUtc = now;
		CompletedEmployees = EmployeeCount;
	}
	public void Fail(string code, DateTime now)
	{
		if (IsTerminal) return;
		Status = code == HRErrorCodes.PayrollPreviewStale ? "SUPERSEDED" : "FAILED";
		ErrorCode = code; CompletedAtUtc = now;
	}
	public void Supersede(DateTime now)
	{
		if (Status is "FAILED" or "SUPERSEDED") return;
		Status = "SUPERSEDED";
		ErrorCode = HRErrorCodes.PayrollPreviewStale;
		CompletedAtUtc = now;
	}
}
