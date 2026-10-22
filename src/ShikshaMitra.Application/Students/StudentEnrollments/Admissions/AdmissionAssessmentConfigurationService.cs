using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentConfigurationService(IRepository<Standard, Guid> standards,
	IRepository<Subject, Guid> subjects) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionWorkflowValidationIssueDto>> ValidateAsync(AdmissionWorkflowStageDefinition stage)
	{
		var issues = new List<AdmissionWorkflowValidationIssueDto>();
		AdmissionAssessmentPolicy? policy;
		try
		{
			policy = AdmissionAssessmentPolicy.Read(stage.ConfigurationJson);
		}
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{
			AddIssue(exception.Message);
			return issues;
		}
		if (policy?.ScoringMode != "subjects") return issues;
		if (policy.EffectiveProfile != null)
		{
			AddIssue("An assessment profile is resolved from the applicant's Offering; remove the saved effective profile from stage settings.");
			return issues;
		}
			var classIds = policy.ClassProfiles.SelectMany(profile => profile.StandardIds).Distinct().ToArray();
			var classes = await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync())
				.Where(item => item.TenantId == CurrentTenant.Id && Enumerable.Contains(classIds, item.Id))
				.Select(item => new { item.Id, MediumId = item.EducationMediumStage.EducationMediumId, BoardId = item.EducationMediumStage.EducationMedium.EducationBoardId }));
			var subjectIds = policy.ClassProfiles.SelectMany(profile => profile.Subjects).Where(item => item.SubjectId.HasValue).Select(item => item.SubjectId!.Value).Distinct().ToArray();
			var catalog = await AsyncExecuter.ToListAsync((await subjects.GetQueryableAsync())
				.Where(item => item.TenantId == CurrentTenant.Id && item.Status == SubjectMasterStatus.Active && Enumerable.Contains(subjectIds, item.Id))
				.Select(item => new { item.Id, item.EducationMediumId }));
			foreach (var profile in policy.ClassProfiles)
			{
				if (profile.StandardIds.Any(id => !classes.Any(item => item.Id == id && item.MediumId == profile.MediumId && item.BoardId == profile.BoardId)))
					AddIssue($"Profile '{profile.Name}' must use classes within its selected school Board and Medium.");
				if (profile.Subjects.Any(subject => subject.SubjectId.HasValue && !catalog.Any(item => item.Id == subject.SubjectId.Value && item.EducationMediumId == profile.MediumId)))
					AddIssue($"Profile '{profile.Name}' references an unavailable or inactive school subject. Choose an active subject from its Medium.");
			}
		return issues;
		void AddIssue(string message) => issues.Add(new AdmissionWorkflowValidationIssueDto { StageId = stage.Id,
			Severity = AdmissionWorkflowValidationSeverity.Error, Code = AdmissionAssessmentErrorCodes.InvalidPolicy,
			Message = $"Stage '{stage.Name}': {message}" });
	}
}
