using System;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SendPayslipDto
{
	public Guid DocumentId { get; set; }
	public Guid IdempotencyKey { get; set; }
}

public record PayslipDeliveryDto(Guid Id, Guid DocumentId, int Revision, string? Recipient,
	string Status, string? ErrorCategory, bool Automatic, Guid? RequestedBy, DateTime RequestedAt,
	DateTime? StartedAt, DateTime? CompletedAt)
{
	public string? RequestedByDisplayName { get; init; }
	public Guid IdempotencyKey { get; init; }
}

public class PayslipDeliveryJobArgs
{
	public Guid? TenantId { get; set; }
	public Guid DeliveryId { get; set; }
}
