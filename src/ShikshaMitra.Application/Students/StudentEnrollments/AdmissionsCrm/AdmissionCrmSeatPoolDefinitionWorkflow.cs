using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatPoolDefinitionWorkflow(
	IRepository<AdmissionCrmPlaybook, Guid> repository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSeatPoolDefinition> ResolveAsync(Guid yearId, UpsertAdmissionCrmSeatPoolDto input)
	{
		if (!input.PlaybookId.HasValue) return new AdmissionCrmSeatPoolDefinition(input.RoundCode, input.Campus,
			input.DesiredClass, input.Board, input.Medium, input.ApplicantCategory, input.OperationalTarget, input.ApprovedCapacity);
		var queryable = await repository.GetQueryableAsync();
		var playbook = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == input.PlaybookId.Value &&
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		if (playbook == null || playbook.Status != AdmissionCrmPlaybookStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedPlaybookRequiredForSeatPool");
		var plans = JsonSerializer.Deserialize<List<AdmissionCrmSeatPlanDto>>(playbook.SeatPlansJson,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
		var plan = plans.FirstOrDefault(x => string.Equals(x.Code, input.Code, StringComparison.OrdinalIgnoreCase))
			?? throw new BusinessException("AdmissionCrm:SeatPlanNotFoundInPlaybook");
		return new AdmissionCrmSeatPoolDefinition(plan.RoundCode, plan.Campus, plan.DesiredClass, plan.Board,
			plan.Medium, plan.ApplicantCategory, plan.PlannedSeats, plan.ApprovedCapacity);
	}
}

public sealed record AdmissionCrmSeatPoolDefinition(string? RoundCode, string? Campus, string? DesiredClass,
	string? Board, string? Medium, string? ApplicantCategory, int OperationalTarget, int ApprovedCapacity);
