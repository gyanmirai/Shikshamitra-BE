using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentRuntimeService(IRepository<AdmissionAssessmentAttempt, Guid> attempts,
	AdmissionAssessmentAccessService access) : ApplicationService, ITransientDependency
{
	public async Task EnsureAccessAsync(Guid applicationId, Guid stageId) => await access.GetContextAsync(applicationId, stageId);
	public async Task<AdmissionAssessmentAttempt?> GetLatestAsync(Guid applicationId, Guid? stageId = null)
	{
		var query = await attempts.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(item => item.ApplicationId == applicationId &&
			(!stageId.HasValue || item.StageInstanceId == stageId.Value) && item.Status != "cancelled")
			.OrderByDescending(item => item.CreationTime).ThenByDescending(item => item.AttemptNumber));
	}
	public async Task<AdmissionAssessmentAttempt?> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		await access.GetContextAsync(applicationId, stageId);
		var attempt = await GetLatestAsync(applicationId, stageId);
		AdmissionAssessmentEvidence.EnsureComplete(attempt);
		return attempt;
	}
	public async Task AddFactsAsync(Guid applicationId, IDictionary<string, string> values, Guid? stageId = null)
	{
		var attempt = await GetLatestAsync(applicationId, stageId);
		if (attempt == null && !stageId.HasValue) return;
		foreach (var key in values.Keys.Where(key => key.StartsWith("assessment.", StringComparison.OrdinalIgnoreCase)).ToArray()) values.Remove(key);
		if (attempt == null) return;
		foreach (var pair in AdmissionAssessmentEvidence.GetFacts(attempt)) values[pair.Key] = pair.Value;
	}
}
