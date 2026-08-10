using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationPolicyReadiness(
	IRepository<AdmissionCrmPolicyAdoption, Guid> adoptionRepository,
	IRepository<AdmissionCrmPolicyPack, Guid> packRepository,
	IRepository<AdmissionCrmPolicyEvaluation, Guid> evaluationRepository)
	: ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<bool> IsReadyAsync(AdmissionInquiryLead lead)
	{
		var adoptionQuery = await adoptionRepository.GetQueryableAsync();
		var adoptions = await AsyncExecuter.ToListAsync(adoptionQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId && x.IsActive));
		if (adoptions.Count == 0) return true;
		var evaluationQuery = await evaluationRepository.GetQueryableAsync();
		var evaluations = await AsyncExecuter.ToListAsync(evaluationQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		foreach (var adoption in adoptions)
		{
			var pack = await packRepository.GetAsync(adoption.PolicyPackId);
			var rules = JsonSerializer.Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson, JsonOptions) ?? [];
			foreach (var rule in rules.Where(x => x.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired))
			{
				if (!string.IsNullOrWhiteSpace(rule.DesiredClass) &&
				    !rule.DesiredClass.Equals(lead.DesiredClassName, StringComparison.OrdinalIgnoreCase)) continue;
				var latest = evaluations.Where(x => x.PolicyAdoptionId == adoption.Id && x.RuleCode == rule.Code.ToUpperInvariant())
					.OrderByDescending(x => x.EvaluatedAt).FirstOrDefault();
				if (latest?.Status is not (AdmissionCrmPolicyEvaluationStatus.Satisfied or
				    AdmissionCrmPolicyEvaluationStatus.ExceptionApproved or AdmissionCrmPolicyEvaluationStatus.NotApplicable))
					return false;
			}
		}
		return true;
	}
}
