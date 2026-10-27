using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Policies;

public class HrSetupReviewQuery
{
	public Guid? EmployeeId { get; set; }
	public DateTime? Date { get; set; }
	public bool NewSample { get; set; }
}

public class RecordHrSetupReviewDto : HrSetupReviewQuery
{
	[Required, StringLength(64, MinimumLength = 64)]
	public string Fingerprint { get; set; } = string.Empty;
	[Required, StringLength(64)]
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class HrSetupReviewDto
{
	public Guid AcademicYearId { get; set; }
	public string Section { get; set; } = string.Empty;
	public string ConfigurationState { get; set; } = "invalid";
	public string ReviewState { get; set; } = "record_not_present";
	public string Fingerprint { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool CanReview { get; set; }
	public bool StarterInitialized { get; set; }
	public bool TenantStarterEnabled { get; set; }
	public Guid? ReviewedBy { get; set; }
	public string? ReviewedByName { get; set; }
	public DateTimeOffset? ReviewedAt { get; set; }
	public List<string> Details { get; set; } = [];
	public List<string> Notices { get; set; } = [];
	public Guid? EmployeeId { get; set; }
	public DateTime? Date { get; set; }
}

public interface IHrSetupReviewAppService : IApplicationService
{
	Task<HrSetupReviewDto> GetAsync(Guid academicYearId, string section, HrSetupReviewQuery input);
	Task<HrSetupReviewDto> RecordAsync(Guid academicYearId, string section, RecordHrSetupReviewDto input);
}
