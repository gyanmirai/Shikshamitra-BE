using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionParentRegistrationAppService
{
	public Task<AdmissionParentStaffContextDto> RevokeAsync(Guid applicationId, AdmissionParentCommand input) => CloseAsync(applicationId, input, "Revoked");
	public Task<AdmissionParentStaffContextDto> TakeoverAsync(Guid applicationId, AdmissionParentCommand input) => CloseAsync(applicationId, input, "TakenOver");
	private Task<AdmissionParentStaffContextDto> CloseAsync(Guid applicationId, AdmissionParentCommand input, string status) =>
		StaffMutationAsync(applicationId, input, async application =>
		{
			var grant = await LatestAsync(applicationId);
			var fingerprint = AdmissionParentRegistration.Hash(status + JsonSerializer.Serialize(input));
			if (grant.IsReplay(input.IdempotencyKey, fingerprint)) return;
			EnsureStamp(application, input);
			grant.Close(status); grant.RecordOperation(input.IdempotencyKey, fingerprint);
			await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
			await data.Repo<AdmissionApplication>().UpdateAsync(application, false);
		});
	public Task<AdmissionParentStaffContextDto> RetryDeliveryAsync(Guid applicationId, AdmissionParentCommand input) =>
		StaffMutationAsync(applicationId, input, async application =>
		{
			var grant = await LatestAsync(applicationId);
			var fingerprint = AdmissionParentRegistration.Hash("retry" + JsonSerializer.Serialize(input));
			if (grant.IsReplay(input.IdempotencyKey, fingerprint)) return;
			EnsureStamp(application, input);
			await access.AuthorizeAsync(grant);
			if (grant.DeliveryStatus is not ("Failed" or "PendingApproval")) throw AdmissionParentRegistrationData.Invalid("Only a known pre-send failure or approval wait can be retried. Resend a new link for an uncertain outcome.");
			grant.SetDelivery("Pending"); grant.RecordOperation(input.IdempotencyKey, fingerprint);
			await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
			await jobs.EnqueueAsync(new AdmissionParentRegistrationDeliveryArgs { TenantId = CurrentTenant.Id, RegistrationId = grant.Id });
			await data.Repo<AdmissionApplication>().UpdateAsync(application, false);
		});
	public Task<AdmissionParentStaffContextDto> ApplyAsync(Guid applicationId, AdmissionParentApplyInput input) =>
		StaffMutationAsync(applicationId, input, async application =>
		{
			var grant = await LatestAsync(applicationId);
			var fingerprint = AdmissionParentRegistration.Hash("apply" + JsonSerializer.Serialize(input));
			if (grant.IsReplay(input.IdempotencyKey, fingerprint)) return;
			EnsureStamp(application, input);
			await access.AuthorizeAsync(grant, requireLiveGrant: false);
			if (grant.Status != "ReadyForReview" || grant.ProposalBaseline == null || grant.ProposalBaseline != await data.BaselineAsync(application))
				throw AdmissionParentRegistrationData.Conflict();
			var values = JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!;
			if (values.Count == 0) throw AdmissionParentRegistrationData.Invalid("No proposed form changes are awaiting application.");
			await formOwner.CorrectParentProposalAsync(applicationId, new CorrectAdmissionApplicationFormDto { Values = values,
				ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = input.IdempotencyKey, Reason = input.Reason });
			grant.Close("Applied"); grant.RecordOperation(input.IdempotencyKey, fingerprint);
			await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
		});
	private async Task<AdmissionParentRegistration> LatestAsync(Guid applicationId) =>
		(await data.Repo<AdmissionParentRegistration>().GetListAsync(row => row.ApplicationId == applicationId)).OrderByDescending(row => row.CreationTime)
			.ThenByDescending(row => row.Id).FirstOrDefault() ?? throw AdmissionParentRegistrationData.Invalid("No parent registration request exists.");
}
