using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance;

public sealed class FinanceWorkContextDto
{
	public Guid AcademicYearId { get; set; }
	public string ScopeLabel { get; set; } = string.Empty;
	public bool CanViewAllTenant { get; set; }
	public List<FinanceScopeOptionDto> ScopeOptions { get; set; } = [];
	public Dictionary<string, FinanceRequestCapabilityDto> RequestCapabilities { get; set; } = [];
}

public sealed class FinanceScopeOptionDto
{
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
}

public sealed class FinanceRequestCapabilityDto
{
	public bool View { get; set; }
	public bool Add { get; set; }
	public bool Submit { get; set; }
	public bool Approve { get; set; }
	public bool Reject { get; set; }
}

public interface IFinanceWorkContextAppService : IApplicationService
{
	Task<FinanceWorkContextDto> GetAsync(Guid academicYearId);
}
