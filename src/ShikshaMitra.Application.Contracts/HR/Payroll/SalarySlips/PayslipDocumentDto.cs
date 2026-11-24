using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public record PayslipDocumentDto(Guid Id, Guid SalarySlipId, int Revision, DateTime IssuedAt,
	Guid? IssuedBy, string Sha256, bool IsCurrent, Guid? ReplacesDocumentId, string? ReplacementReason)
{
	public string? RecipientEmail { get; init; }
	public string PaymentStatus { get; init; } = string.Empty;
	public DateTimeOffset? PaidAt { get; init; }
	public string? PaymentReference { get; init; }
}

public class RecordPayslipPaymentDto
{
	public Guid DocumentId { get; set; }
	public Guid IdempotencyKey { get; set; }
	public DateTimeOffset PaidAt { get; set; }
	[Required, StringLength(200)] public string Reference { get; set; } = string.Empty;
}

public class ReplacePayslipFactsDto
{
	public Guid DocumentId { get; set; }
	public Guid IdempotencyKey { get; set; }
	[Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class IssuedPayslipListInput : PagedResultRequestDto
{
	[StringLength(7)] public string? MonthYear { get; set; }
	[StringLength(100)] public string? Search { get; set; }
}

public record IssuedPayslipListItem(Guid Id, Guid EmployeeId, string EmployeeName, string? EmployeeCode,
	string MonthYear, int Revision, DateTime IssuedAt, bool IsPaid);

public interface IPayslipDocumentAppService : IApplicationService
{
	Task<PagedResultDto<IssuedPayslipListItem>> GetIssuedAsync(IssuedPayslipListInput input);
	Task<List<PayslipDocumentDto>> GetAsync(Guid employeeId, string monthYear);
	Task<byte[]> GetPdfAsync(Guid documentId);
	Task RecordPaymentAsync(RecordPayslipPaymentDto input);
	Task<PayslipDocumentDto> ReplaceFactsAsync(ReplacePayslipFactsDto input);
}
