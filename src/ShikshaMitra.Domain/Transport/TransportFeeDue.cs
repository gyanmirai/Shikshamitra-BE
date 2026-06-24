using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportFeeDue : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid FeeRunId { get; private set; }
	public Guid FeeConfigurationId { get; private set; }
	public Guid ChargeCategoryId { get; private set; }
	public Guid MemberId { get; private set; }
	public TransportMemberType MemberType { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? EmployeeId { get; private set; }
	public Guid AssignmentId { get; private set; }
	public Guid RouteId { get; private set; }
	public Guid StopId { get; private set; }
	public TransportFeeModelType FeeModelType { get; private set; }
	public TransportFeeFrequency FeeFrequency { get; private set; }
	public DateTime PeriodFrom { get; private set; }
	public DateTime PeriodTo { get; private set; }
	public DateTime DueDate { get; private set; }
	public decimal Amount { get; private set; }
	public TransportFeeDueStatus Status { get; private set; }
	public Guid? FinanceChargeRequestId { get; private set; }
	public Guid? FinanceChargeLineId { get; private set; }
	public DateTime? FinancePostedAt { get; private set; }
	public string MemberName { get; private set; } = string.Empty;
	public string? MemberCode { get; private set; }
	public string RouteName { get; private set; } = string.Empty;
	public string RouteCode { get; private set; } = string.Empty;
	public string StopName { get; private set; } = string.Empty;

	private TransportFeeDue()
	{
	}

	public TransportFeeDue(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid academicYearId,
		Guid feeRunId,
		Guid feeConfigurationId,
		Guid chargeCategoryId,
		Guid memberId,
		TransportMemberType memberType,
		Guid? studentId,
		Guid? employeeId,
		Guid assignmentId,
		Guid routeId,
		Guid stopId,
		TransportFeeModelType feeModelType,
		TransportFeeFrequency feeFrequency,
		DateTime periodFrom,
		DateTime periodTo,
		DateTime dueDate,
		decimal amount,
		string memberName,
		string? memberCode,
		string routeName,
		string routeCode,
		string stopName) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		AcademicYearId = academicYearId;
		FeeRunId = feeRunId;
		FeeConfigurationId = feeConfigurationId;
		ChargeCategoryId = chargeCategoryId;
		MemberId = memberId;
		MemberType = memberType;
		StudentId = studentId;
		EmployeeId = employeeId;
		AssignmentId = assignmentId;
		RouteId = routeId;
		StopId = stopId;
		FeeModelType = feeModelType;
		FeeFrequency = feeFrequency;
		PeriodFrom = periodFrom.Date;
		PeriodTo = periodTo.Date;
		DueDate = dueDate.Date;
		Amount = amount;
		Status = TransportFeeDueStatus.Pending;
		MemberName = memberName.Trim();
		MemberCode = NormalizeNullable(memberCode);
		RouteName = routeName.Trim();
		RouteCode = routeCode.Trim();
		StopName = stopName.Trim();
	}

	public void MarkPostedToFinance(Guid chargeRequestId, Guid chargeLineId)
	{
		FinanceChargeRequestId = chargeRequestId;
		FinanceChargeLineId = chargeLineId;
		FinancePostedAt = DateTime.UtcNow;
		Status = TransportFeeDueStatus.PostedToFinance;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
