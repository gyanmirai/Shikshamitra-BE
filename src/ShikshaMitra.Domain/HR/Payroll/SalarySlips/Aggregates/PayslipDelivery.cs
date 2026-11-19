using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipDelivery : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid DocumentId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid RequestId { get; private set; }
	public bool Automatic { get; private set; }
	public string? Recipient { get; private set; }
	public string Status { get; private set; } = "QUEUED";
	public string? ErrorCategory { get; private set; }
	public DateTime? StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public Guid? RequestedBy { get; private set; }
	public string RequiredPermission { get; private set; } = string.Empty;

	private PayslipDelivery() { }
	public PayslipDelivery(Guid id, Guid? tenantId, Guid documentId, Guid employeeId, Guid requestId,
		bool automatic, Guid? requestedBy, string requiredPermission) : base(id)
	{
		if (requestId == Guid.Empty) throw new BusinessException(HRErrorCodes.PayslipConflict);
		TenantId = tenantId;
		DocumentId = documentId;
		EmployeeId = employeeId;
		RequestId = requestId;
		Automatic = automatic;
		RequestedBy = requestedBy;
		RequiredPermission = requiredPermission;
	}

	public void Start(string recipient, DateTime at)
	{
		if (Status != "QUEUED") throw new BusinessException(HRErrorCodes.PayslipConflict);
		Recipient = recipient;
		StartedAt = at;
		Status = "PROCESSING";
	}

	public void Finish(string status, DateTime at, string? errorCategory = null)
	{
		if (Status is not ("QUEUED" or "PROCESSING")) throw new BusinessException(HRErrorCodes.PayslipConflict);
		if (status is not ("PROVIDER_ACCEPTED" or "FAILED" or "UNCERTAIN" or "SUPERSEDED"))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		Status = status;
		CompletedAt = at;
		ErrorCategory = errorCategory;
	}
}
