using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>Staged correction evidence; an issued document is never edited while this draft is prepared.</summary>
public class PayslipCorrection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SalarySlipId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid SourceDocumentId { get; private set; }
	public string SourceMonth { get; private set; } = string.Empty;
	public string Reason { get; private set; } = string.Empty;
	public Guid RequestId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string OriginalSnapshotJson { get; private set; } = string.Empty;
	public string AttendanceJson { get; private set; } = "[]";
	public string? CandidateJson { get; private set; }
	public Guid? InputRevision { get; private set; }
	public string Status { get; private set; } = "DRAFT";
	public Guid? ReviewedBy { get; private set; }
	public DateTime? ReviewedAt { get; private set; }
	public Guid? ResultDocumentId { get; private set; }
	public string? SettlementMonth { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public Guid? CompletedBy { get; private set; }
	public bool IsActive => Status is "DRAFT" or "CALCULATED" or "REVIEWED";

	private PayslipCorrection() { }
	public PayslipCorrection(Guid id, PayslipDocument source, Guid academicYearId, string reason,
		Guid requestId, string originalSnapshotJson) : base(id)
	{
		if (id == Guid.Empty || academicYearId == Guid.Empty || requestId == Guid.Empty ||
			string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || string.IsNullOrWhiteSpace(originalSnapshotJson))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		TenantId = source.TenantId;
		SalarySlipId = source.SalarySlipId;
		EmployeeId = source.EmployeeId;
		SourceDocumentId = source.Id;
		SourceMonth = source.MonthYear;
		AcademicYearId = academicYearId;
		Reason = reason.Trim();
		RequestId = requestId;
		OriginalSnapshotJson = originalSnapshotJson;
	}

	public void Calculate(string attendanceJson, string candidateJson, Guid inputRevision)
	{
		EnsureActive();
		if (string.IsNullOrWhiteSpace(attendanceJson) || string.IsNullOrWhiteSpace(candidateJson))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		AttendanceJson = attendanceJson;
		CandidateJson = candidateJson;
		InputRevision = inputRevision;
		ReviewedBy = null;
		ReviewedAt = null;
		Status = "CALCULATED";
	}

	public void InitializeAttendance(string attendanceJson)
	{
		if (Status != "DRAFT" || string.IsNullOrWhiteSpace(attendanceJson))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		AttendanceJson = attendanceJson;
	}

	public void Review(Guid inputRevision, Guid actor, DateTime now)
	{
		if (Status != "CALCULATED" || InputRevision != inputRevision || actor == Guid.Empty)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		ReviewedBy = actor;
		ReviewedAt = now;
		Status = "REVIEWED";
	}

	public void Complete(Guid inputRevision, Guid actor, DateTime now, Guid? resultDocumentId, string? settlementMonth)
	{
		if (Status != "REVIEWED" || InputRevision != inputRevision || actor == Guid.Empty ||
			(resultDocumentId.HasValue == !string.IsNullOrWhiteSpace(settlementMonth)) || resultDocumentId == Guid.Empty ||
			(settlementMonth != null && (!DateTime.TryParseExact(settlementMonth + "-01", "yyyy-MM-dd", null,
				System.Globalization.DateTimeStyles.None, out _) || string.CompareOrdinal(settlementMonth, SourceMonth) <= 0)))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		ResultDocumentId = resultDocumentId;
		SettlementMonth = settlementMonth;
		CompletedBy = actor;
		CompletedAt = now;
		Status = "APPLIED";
	}

	public void Cancel(Guid actor, DateTime now)
	{
		EnsureActive();
		if (actor == Guid.Empty) throw new BusinessException(HRErrorCodes.PayslipConflict);
		CompletedBy = actor;
		CompletedAt = now;
		Status = "CANCELLED";
	}

	private void EnsureActive()
	{
		if (!IsActive) throw new BusinessException(HRErrorCodes.PayslipConflict);
	}
}
