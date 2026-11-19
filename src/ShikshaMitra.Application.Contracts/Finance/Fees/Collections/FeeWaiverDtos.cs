using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public interface IFeeWaiverAppService : IApplicationService
{
	Task<FeeWaiverResultDto> ApplyAsync(ApplyFeeWaiverInput input);
}

public class ApplyFeeWaiverInput
{
	/// <summary>LateFee or Charge.</summary>
	public string SourceType { get; set; } = string.Empty;
	/// <summary>DueFee id for LateFee; Charge line id for Charge.</summary>
	public Guid SourceId { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string IdempotencyKey { get; set; } = string.Empty;
}

public class FeeWaiverResultDto
{
	public Guid Id { get; set; }
	public string SourceType { get; set; } = string.Empty;
	public Guid SourceId { get; set; }
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public decimal WaivedAmount { get; set; }
	public decimal RemainingAmount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public DateTime CreationTime { get; set; }
}
