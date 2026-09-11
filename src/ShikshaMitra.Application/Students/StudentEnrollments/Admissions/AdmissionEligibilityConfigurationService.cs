using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilityConfigurationService(IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionFormFieldDefinition, Guid> fields, IRepository<IdentityRole, Guid> roles,
	AdmissionEligibilitySourceStageService sources) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionWorkflowValidationIssueDto>> ValidateAsync(AdmissionWorkflowStageDefinition stage)
	{
		var issues = new List<AdmissionWorkflowValidationIssueDto>();
		try
		{
			var policy = AdmissionEligibilityAccessService.ReadPolicy(stage);
			if (policy == null) return issues;
			if (!string.IsNullOrWhiteSpace(policy.ExceptionApprovalRole) &&
				!await roles.AnyAsync(role => role.NormalizedName == policy.ExceptionApprovalRole.ToUpperInvariant()))
				throw new ArgumentException("Select an existing exception approval role for this school.");
			foreach (var criterion in policy.Criteria)
			{
				if (criterion.Kind is "application" or "inquiry")
				{
					var form = await forms.FindAsync(criterion.FormDefinitionId!.Value);
					if (form?.Status != AdmissionFormStatus.Published) throw new ArgumentException($"{criterion.Name}: select a published form in this school.");
					var rows = await fields.GetListAsync(item => item.FormDefinitionId == form.Id);
					var names = rows.ToDictionary(item => criterion.Kind + "." + (criterion.Kind == "inquiry" && !string.IsNullOrWhiteSpace(item.BindingKey) ? item.BindingKey : item.Code), item => item.FieldType, StringComparer.OrdinalIgnoreCase);
					AdmissionEligibilityConditionSchema.Validate(criterion.ConditionJson!, names);
				}
				if (criterion.Kind is "assessment" or "documents")
				{
					var source = await sources.FindDefinitionAsync(stage, criterion);
					if (source == null) throw new ArgumentException($"{criterion.Name}: choose an earlier managed {criterion.Kind} stage in this workflow.");
					if (criterion.MinimumPercentage.HasValue && AdmissionAssessmentAccessService.ReadPolicy(source)?.ScoringEnabled != true)
						throw new ArgumentException($"{criterion.Name}: the assessment source must have scoring enabled.");
				}
			}
		}
		catch (Exception exception) when (exception is ArgumentException or BusinessException)
		{
			issues.Add(new() { Code = AdmissionEligibilityErrorCodes.InvalidPolicy, Message = exception.Message,
				Severity = AdmissionWorkflowValidationSeverity.Error, StageId = stage.Id });
		}
		return issues;
	}
	public async Task<List<AdmissionEligibilityRoleDto>> GetRolesAsync() =>
		(await roles.GetListAsync()).OrderBy(role => role.Name).Select(role => new AdmissionEligibilityRoleDto { Id = role.Id, Name = role.Name }).ToList();
}
