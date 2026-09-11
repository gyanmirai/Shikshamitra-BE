using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityState(AdmissionEligibilityContext Context, AdmissionEligibilityEvaluation? Latest,
	AdmissionEligibilityCheck? Check, List<AdmissionEligibilityException> Exceptions)
{
	public bool IsStale => Latest != null && Check != null && Latest.InputFingerprint != Check.Fingerprint;
	public string Status => Latest == null ? "notChecked" : Latest.Outcome == "ineligible" && Exceptions.Any(item => item.EvaluationId == Latest.Id && item.Decision == "approved") ? "exceptionApproved" : Latest.Outcome;
	public bool Passed => Latest != null && !IsStale && Status is "eligible" or "exceptionApproved";
	public List<AdmissionEligibilityException> Pending => Exceptions.Where(item => item.EvaluationId == Latest?.Id && item.Decision == "pending" && !Exceptions.Any(decision => decision.RequestId == item.Id)).ToList();
	public void EnsureFresh()
	{
		if (Latest == null) throw new BusinessException(AdmissionEligibilityErrorCodes.EvaluationRequired);
		if (IsStale) throw new BusinessException(AdmissionEligibilityErrorCodes.StaleEvaluation);
	}
}

[RemoteService(false)]
public class AdmissionEligibilityStateService(AdmissionEligibilityEvaluationService evaluator,
	IRepository<AdmissionEligibilityEvaluation, Guid> evaluations, IRepository<AdmissionEligibilityException, Guid> exceptions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionEligibilityState> ReadAsync(AdmissionEligibilityContext context)
	{
		var query = await evaluations.GetQueryableAsync();
		var latest = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item => item.ApplicationId == context.Application.Id && item.StageInstanceId == context.Stage.Id).OrderByDescending(item => item.SequenceNumber));
		var check = context.Policy == null || context.Application.Status == AdmissionApplicationStatus.AdmissionFinalized ? null : await evaluator.CheckAsync(context);
		var decisions = await exceptions.GetListAsync(item => item.ApplicationId == context.Application.Id && item.StageInstanceId == context.Stage.Id);
		return new(context, latest, check, decisions);
	}
}
