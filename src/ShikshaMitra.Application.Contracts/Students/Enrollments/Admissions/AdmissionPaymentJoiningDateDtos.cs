using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPaymentJoiningDateAppService : IApplicationService
{
	Task<AdmissionPaymentJoiningDateDto> GetAsync(Guid applicationId);
	Task<AdmissionPaymentJoiningDatePreviewDto> PreviewAsync(Guid applicationId, PreviewAdmissionPaymentJoiningDateDto input);
	Task<AdmissionPaymentJoiningDateDto> SaveAsync(Guid applicationId, SaveAdmissionPaymentJoiningDateDto input);
}

public class AdmissionPaymentJoiningDateDto
{
	public DateTime? JoiningDate { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool CanEdit { get; set; }
	public bool CanPreview { get; set; }
	public bool IsLocked { get; set; }
	public string? LockReason { get; set; }
	public DateTime AcademicYearStartDate { get; set; }
	public DateTime AcademicYearEndDate { get; set; }
	public bool CanSelectProrationPolicy { get; set; }
	public Guid? DefaultProrationPolicyVersionId { get; set; }
	public List<AdmissionProrationPolicyOptionDto> ProrationPolicies { get; set; } = [];
}

public class AdmissionProrationPolicyOptionDto
{
	public Guid PolicyId { get; set; }
	public Guid PolicyVersionId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public int PolicyVersion { get; set; }
	public bool IsDefault { get; set; }
}

public class PreviewAdmissionPaymentJoiningDateDto
{
	[Required, RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
	public string JoiningDate { get; set; } = string.Empty;
	public List<VariableFeeHeadAmountDto> VariableFeeHeads { get; set; } = [];
	public Guid? ProrationPolicyVersionId { get; set; }
}

public class SaveAdmissionPaymentJoiningDateDto
{
	[Required, RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
	public string JoiningDate { get; set; } = string.Empty;
	[Required, MaxLength(100)]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionPaymentJoiningDatePreviewDto
{
	public DateTime JoiningDate { get; set; }
	public decimal TotalAmount { get; set; }
	public decimal ScheduledAmount { get; set; }
	public decimal ReductionAmount { get; set; }
	public FeeProrationResultDto? Proration { get; set; }
	public AdmissionProrationPolicyDto? ProrationPolicy { get; set; }
	public List<string> Warnings { get; set; } = [];
}
