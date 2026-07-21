using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public interface ILateFeeWaiverAppService : IApplicationService
{
	Task<List<LateFeeWaiverApplicationDto>> GetWaiversForDueFeeAsync(Guid dueFeeId);
	Task<LateFeeWaiverApplicationDto> ApplyWaiverAsync(ApplyLateFeeWaiverInput input);
	Task<LateFeeWaiverApplicationDto> ApproveAsync(Guid id);
	Task<LateFeeWaiverApplicationDto> RejectAsync(Guid id);
}

public class ApplyLateFeeWaiverInput
{
	public Guid DueFeeId { get; set; }
	/// <summary>Absolute amount requested to be waived. Takes precedence over RequestedPercentage if both are set.</summary>
	public decimal? RequestedAmount { get; set; }
	/// <summary>Percentage (0-100) of the currently-outstanding computed late fee to waive.</summary>
	public decimal? RequestedPercentage { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class LateFeeWaiverApplicationDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public Guid DueFeeId { get; set; }
	public Guid CollectionPolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public decimal ComputedLateFeeAmount { get; set; }
	public decimal RequestedAmount { get; set; }
	public decimal EffectiveAmount { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string Status { get; set; } = "Applied";
	public Guid AppliedByUserId { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public DateTime CreationTime { get; set; }
}
