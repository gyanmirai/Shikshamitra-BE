using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionReviewState(AdmissionReviewContext Context, AdmissionReviewWorkItem? WorkItem,
	AdmissionReviewRecord? Latest, AdmissionReviewDecision? Decision, AdmissionReviewEvidence? Evidence)
{
	public bool IsStale => Latest != null && Evidence != null && Latest.InputFingerprint != Evidence.Fingerprint;
	public Guid? AssignedReviewerId => WorkItem?.AssignedReviewerId ?? (WorkItem == null ? Context.Policy?.AssignedReviewerId : null);
	public DateTime? DueAt => WorkItem != null ? WorkItem.DueAt : Context.Policy?.DueInDays is int days ? Context.Stage.EnteredAt?.AddDays(days) : null;
	public bool IsOverdue => DueAt < DateTime.UtcNow && Decision == null;
	public string Status => IsStale ? "stale" : Decision?.Decision switch
	{
		"approve" => "approved", "waitlist" => "waitlisted", "reject" => "rejected", "requestCorrection" => "correctionRequested",
		_ => Latest == null ? "notStarted" : Latest.Status == "assignment" ? "draft" : Latest.Status
	};
	public void EnsureSubmitted()
	{
		if (Latest?.Status != "submitted") throw new BusinessException(AdmissionReviewErrorCodes.ReviewRequired);
		if (IsStale) throw new BusinessException(AdmissionReviewErrorCodes.StaleReview);
		if (Evidence?.IsKnown != true) throw new BusinessException(AdmissionReviewErrorCodes.EvidenceRequired);
	}
}

[RemoteService(false)]
public class AdmissionReviewStateService(AdmissionReviewEvidenceService evidence,
	IRepository<AdmissionReviewRecord, Guid> records, IRepository<AdmissionReviewDecision, Guid> decisions,
	IRepository<AdmissionReviewWorkItem, Guid> workItems) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionReviewState> ReadAsync(AdmissionReviewContext context)
	{
		var query = await records.GetQueryableAsync();
		var latest = await AsyncExecuter.FirstOrDefaultAsync(query.Where(item => item.ApplicationId == context.Application.Id && item.StageInstanceId == context.Stage.Id).OrderByDescending(item => item.SequenceNumber));
		var decision = latest == null ? null : await decisions.FindAsync(item => item.RecordId == latest.Id);
		var work = await workItems.FindAsync(item => item.ApplicationId == context.Application.Id && item.StageInstanceId == context.Stage.Id);
		var snapshot = context.Policy == null ? null : context.Application.Status == AdmissionApplicationStatus.AdmissionFinalized && latest != null
			? new AdmissionReviewEvidence(latest.InputFingerprint, latest.PolicySnapshotJson,
				JsonSerializer.Deserialize<System.Collections.Generic.List<AdmissionReviewEvidenceDto>>(latest.EvidenceSnapshotJson, AdmissionReviewEvidenceService.JsonOptions) ?? [])
			: await evidence.ReadAsync(context);
		return new(context, work, latest, decision, snapshot);
	}
}
