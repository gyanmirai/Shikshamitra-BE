using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[AllowAnonymous, RemoteService(false), DisableAuditing]
public partial class AdmissionParentRegistrationPublicAppService(AdmissionParentRegistrationData data,
	AdmissionParentRegistrationAccess access, AdmissionParentRegistrationForms forms, AdmissionParentRegistrationDocuments documents,
	AdmissionApplicationFormService formOwner, ICurrentPrincipalAccessor principals)
	: ApplicationService, IAdmissionParentRegistrationPublicAppService
{
	public async Task<AdmissionParentPublicContextDto> GetAsync(string token)
	{
		using var anonymous = principals.Change(new ClaimsPrincipal(new ClaimsIdentity()));
		var grant = await access.ResolveAsync(token);
		using var tenant = CurrentTenant.Change(grant.TenantId);
		return await ContextAsync(grant, await access.AuthorizeAsync(grant));
	}
	public Task<AdmissionParentPublicContextDto> SaveAsync(string token, AdmissionParentDraftInput input) =>
		MutateAsync(token, input, "save", input, async (grant, application) =>
		{
			var form = await data.FormAsync(application);
			var writable = AdmissionParentRegistrationForms.Editable(form, grant, await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId));
			AdmissionParentRegistrationForms.CheckPatch(form, writable, input.Values);
			var proposal = application.SubmittedAt.HasValue;
			var baseline = await data.BaselineAsync(application);
			if (proposal && grant.ProposalBaseline != null && grant.ProposalBaseline != baseline) throw AdmissionParentRegistrationData.Conflict();
			var patch = proposal ? AdmissionParentRegistrationForms.Merge(JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!, input.Values) : input.Values;
			var merged = AdmissionParentRegistrationForms.Merge(form.Values, patch);
			var previous = proposal ? AdmissionParentRegistrationForms.Merge(form.Values, JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!) : form.Values;
			await forms.ValidateAsync(application, form with { Values = previous }, merged, false, writable, input.Values.Select(value => value.FieldCode).ToHashSet(StringComparer.OrdinalIgnoreCase));
			if (proposal) grant.Save(Clock.Now, JsonSerializer.Serialize(AdmissionParentRegistrationForms.VisibleProposals(form, patch,
				LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationFormValidator>())), baseline);
			else
			{
				await formOwner.SaveParentDraftAsync(application, grant, form, input.Values, writable);
				grant.Save(Clock.Now);
			}
		});
	public Task<AdmissionParentPublicContextDto> HandInAsync(string token, AdmissionParentPublicCommand input) =>
		MutateAsync(token, input, "hand-in", input, async (grant, application) =>
		{
			var form = await data.FormAsync(application);
			if (application.SubmittedAt.HasValue && grant.ProposalBaseline != null && grant.ProposalBaseline != await data.BaselineAsync(application))
				throw AdmissionParentRegistrationData.Conflict();
			var merged = AdmissionParentRegistrationForms.Merge(form.Values, JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!);
			await forms.ValidateAsync(application, form, merged, true, AdmissionParentRegistrationForms.Editable(form, grant, await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId)));
			await CheckDeadlineAsync(application);
			var requirements = await documents.ReadAsync(application, grant);
			var missing = requirements.Where(item => item.Dto.IsRequired && (item.Dto.Status == "missing" || !AdmissionParentRegistrationDocuments.HasParentEvidence(item.Evaluation)))
				.Select(item => item.Dto.Name).ToList();
			if (missing.Count > 0) throw new BusinessException(AdmissionParentRegistrationErrors.Incomplete, "Upload the required documents before sending to school.").WithData("Documents", missing);
			grant.HandIn(Clock.Now);
			await AuditAsync(grant, application, "ParentRegistrationReadyForReview");
		});
	private async Task<AdmissionParentPublicContextDto> MutateAsync(string token, AdmissionParentPublicCommand input,
		string operation, object payload, Func<AdmissionParentRegistration, AdmissionApplication, Task> action)
	{
		using var anonymous = principals.Change(new ClaimsPrincipal(new ClaimsIdentity()));
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
			var grant = await access.ResolveAsync(token);
			using var tenant = CurrentTenant.Change(grant.TenantId);
			var application = await access.AuthorizeAsync(grant);
			var fingerprint = AdmissionParentRegistration.Hash(operation + JsonSerializer.Serialize(payload));
			if (!grant.IsReplay(input.IdempotencyKey, fingerprint))
			{
				await access.EnsureSnapshotAsync(grant, application, input.SnapshotToken);
				if (grant.ProposalBaseline != null && grant.ProposalBaseline != await data.BaselineAsync(application)) throw AdmissionParentRegistrationData.Conflict();
				await action(grant, application);
				grant.RecordOperation(input.IdempotencyKey, fingerprint);
				await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
				// Application CAS participates even for proposals/documents; staff can still save normally.
				await data.Repo<AdmissionApplication>().UpdateAsync(application, false);
				await work.SaveChangesAsync();
				if (grant.ProposalBaseline != null)
				{
					grant.RebaseProposal(await data.BaselineAsync(application));
					await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false);
					await work.SaveChangesAsync();
				}
			}
			var context = await ContextAsync(grant, application);
			await work.CompleteAsync();
			return context;
		}
		catch (AbpDbConcurrencyException) { throw AdmissionParentRegistrationData.Conflict(); }
		catch (Exception error) when (IsDatabaseConflict(error)) { throw AdmissionParentRegistrationData.Conflict(); }
	}
	private async Task<AdmissionParentPublicContextDto> ContextAsync(AdmissionParentRegistration grant, AdmissionApplication application)
	{
		var form = await data.FormAsync(application);
		var editable = AdmissionParentRegistrationForms.Editable(form, grant, await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId));
		var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
		var proposals = JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!;
		var merged = AdmissionParentRegistrationForms.Merge(form.Values, proposals);
		var definition = await forms.ProjectAsync(application, form with { Values = merged }, editable);
		var exposed = definition.Sections.SelectMany(section => section.Fields).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var tenantName = CurrentTenant.Id.HasValue ? (await LazyServiceProvider.LazyGetRequiredService<Volo.Abp.TenantManagement.ITenantRepository>().FindAsync(CurrentTenant.Id.Value))?.Name : null;
		return new() { SchoolName = tenantName ?? "School admission", ApplicantName = string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
			ApplicationNumber = application.ApplicationNumber, Purpose = grant.Purpose, Status = grant.Status, ExpiresAt = grant.ExpiresAt,
			SnapshotToken = await data.SnapshotAsync(grant, application), CanEdit = grant.CanWrite(Clock.Now), CanHandIn = grant.CanWrite(Clock.Now),
			ProposalOnly = application.SubmittedAt.HasValue, Notes = grant.Notes, Definition = definition, EditableFieldCodes = editable.ToList(),
			Values = merged.Where(value => exposed.Contains(value.FieldCode)).ToList(), Documents = (await documents.ReadAsync(application, grant)).Select(item => item.Dto).ToList() };
	}
	private async Task CheckDeadlineAsync(AdmissionApplication application)
	{
		if (!application.CurrentStageInstanceId.HasValue) return;
		var stage = await data.Repo<AdmissionApplicationStageInstance>().GetAsync(application.CurrentStageInstanceId.Value);
		if (stage.StageType != AdmissionWorkflowStageType.Form) return;
		var definition = await data.Repo<AdmissionWorkflowStageDefinition>().GetAsync(stage.DefinitionStageId);
		var policy = AdmissionFormStageAccessService.ReadPolicy(definition);
		if (policy != null && !policy.AllowLateSubmission && policy.DueInDays.HasValue && stage.EnteredAt?.AddDays(policy.DueInDays.Value) < Clock.Now)
			throw AdmissionParentRegistrationData.Invalid("The registration deadline has passed. Contact the school.");
	}
	private Task AuditAsync(AdmissionParentRegistration grant, AdmissionApplication application, string type) =>
		data.Repo<AdmissionActivity>().InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, type, "Parent registration handed to school for review", application.ApplicantId, application.Id, null, JsonSerializer.Serialize(new { ParentRegistrationId = grant.Id,
				Recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson) })), false);
	private static bool IsDatabaseConflict(Exception error)
	{
		for (Exception? current = error; current != null; current = current.InnerException)
			if (current is System.Data.Common.DbException database && database.SqlState is "40001" or "40P01" or "23505") return true;
		return false;
	}
}
