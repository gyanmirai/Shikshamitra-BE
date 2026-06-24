using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportFeeRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid FeeConfigurationId { get; private set; }
	public TransportFeeRunStatus Status { get; private set; }
	public DateTime PeriodFrom { get; private set; }
	public DateTime PeriodTo { get; private set; }
	public DateTime DueDate { get; private set; }
	public int GeneratedDueCount { get; private set; }
	public int StudentDueCount { get; private set; }
	public int EmployeeDueCount { get; private set; }
	public decimal TotalAmount { get; private set; }
	public DateTime? FinancePostedAt { get; private set; }
	public int FinanceChargeRequestCount { get; private set; }
	public string? Remarks { get; private set; }

	private TransportFeeRun()
	{
	}

	public TransportFeeRun(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid academicYearId,
		Guid feeConfigurationId,
		DateTime periodFrom,
		DateTime periodTo,
		DateTime dueDate,
		int generatedDueCount,
		int studentDueCount,
		int employeeDueCount,
		decimal totalAmount,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		AcademicYearId = academicYearId;
		FeeConfigurationId = feeConfigurationId;
		Status = TransportFeeRunStatus.Generated;
		PeriodFrom = periodFrom.Date;
		PeriodTo = periodTo.Date;
		DueDate = dueDate.Date;
		GeneratedDueCount = generatedDueCount;
		StudentDueCount = studentDueCount;
		EmployeeDueCount = employeeDueCount;
		TotalAmount = totalAmount;
		Remarks = NormalizeNullable(remarks);
	}

	public void MarkFinancePosted(int chargeRequestCount, bool hasUnpostedStudentDues)
	{
		FinancePostedAt = DateTime.UtcNow;
		FinanceChargeRequestCount += chargeRequestCount;
		Status = hasUnpostedStudentDues ? TransportFeeRunStatus.PartiallyPosted : TransportFeeRunStatus.FinancePosted;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
