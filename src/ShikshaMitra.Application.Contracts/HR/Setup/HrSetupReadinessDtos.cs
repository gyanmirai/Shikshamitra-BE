using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Setup;

public class HrSetupCheckDto
{
	public string Code { get; set; } = string.Empty;
	public string Step { get; set; } = string.Empty;
	public string State { get; set; } = "missing";
	public string Reason { get; set; } = string.Empty;
	public bool CanConfigure { get; set; }
}

public class HrSetupYearDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}

public class HrSetupReadinessDto
{
	public Guid? AcademicYearId { get; set; }
	public bool IsReady { get; set; }
	public List<HrSetupYearDto> AvailableYears { get; set; } = [];
	public List<HrSetupCheckDto> Checks { get; set; } = [];
}

public interface IHrSetupReadinessAppService : IApplicationService
{
	Task<HrSetupReadinessDto> GetAsync(Guid? academicYearId = null);
}
