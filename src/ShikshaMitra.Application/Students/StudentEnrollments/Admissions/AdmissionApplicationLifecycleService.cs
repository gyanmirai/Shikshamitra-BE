using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationLifecycleService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionInquiry, Guid> inquiryRepository,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> fieldRepository,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowInstanceRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageInstanceRepository,
	IRepository<AdmissionFormInstance, Guid> formInstanceRepository,
	IRepository<AdmissionFormFieldValue, Guid> valueRepository,
	IRepository<AdmissionFormChangeHistory, Guid> historyRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	IAdmissionReferenceSequenceRepository referenceSequenceRepository,
	AdmissionOfferingRuntimeService offeringRuntimeService,
	AdmissionInquiryRuntimeService inquiryRuntime,
	AdmissionWorkflowStageHandlerRegistry stageHandlerRegistry)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId)
	{
		var existing = await FindExistingAsync(applicantId, offeringId);
		if (existing != null) return AdmissionApplicationMapper.ToDto(existing);
		var applicant = await applicantRepository.FindAsync(applicantId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicant), applicantId);
		var offering = (await offeringRuntimeService.GetAvailableAsync(offeringId)).Offering;
		inquiryId = await ResolveInquiryAsync(applicantId, offeringId, inquiryId);
		var inquiry = await EnsureInquiryMatchesAsync(inquiryId, applicantId, offering);
		if (offering.EntryMode == AdmissionEntryMode.Inquiry && inquiry == null)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.LinkedInquiryRequired,
				"Start with an inquiry for this Admission Offering.");
		if (offering.EntryMode == AdmissionEntryMode.DirectRegistration && inquiry != null)
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable,
				"This Admission Offering starts directly with Registration.");
		await inquiryRuntime.EnsureCanStartApplicationAsync(offering.Id, inquiry);
		var workflow = await workflowRepository.FindAsync(offering.WorkflowDefinitionId!.Value);
		var form = await formRepository.FindAsync(offering.ApplicationFormDefinitionId!.Value);
		if (workflow == null || !workflow.Status.IsRuntimeVersion() || form?.Status != AdmissionFormStatus.Published)
			throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);

		var referenceYear = Clock.Now.ToUniversalTime().Year;
		var applicationNumber = AdmissionReferenceNumber.Format(
			AdmissionReferenceKind.Application,
			referenceYear,
			await referenceSequenceRepository.IncrementAndGetAsync(referenceYear, AdmissionReferenceKind.Application));
		var application = new AdmissionApplication(
			GuidGenerator.Create(), CurrentTenant.Id, applicationNumber, applicantId, inquiryId,
			offering.AdmissionCycleId, offeringId, workflow.Id, form.Id);
		if (inquiry?.AssignedCounsellorId is { } counsellorId)
			application.AssignCounsellor(counsellorId);
		var workflowInstance = new AdmissionApplicationWorkflowInstance(
			GuidGenerator.Create(), CurrentTenant.Id, application.Id, workflow.Id);
		var stageDefinitions = (await stageRepository.GetListAsync(stage => stage.WorkflowDefinitionId == workflow.Id))
			.OrderBy(stage => stage.DisplayOrder).ToList();
		var entry = stageDefinitions.SingleOrDefault(stage => stage.IsEntryStage) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
		var stages = stageDefinitions.Select(stage => new AdmissionApplicationStageInstance(
			GuidGenerator.Create(), CurrentTenant.Id, workflowInstance.Id, stage.Id, stage.Code,
			stage.Name, stage.StageType, stage.DisplayOrder, stage.Id == entry.Id)).ToList();
		var entryInstance = stages.Single(stage => stage.DefinitionStageId == entry.Id);
		application.SetCurrentStage(entryInstance.Id);
		await stageHandlerRegistry.OnEnterAsync(entryInstance.StageType,
			new AdmissionWorkflowStageRuntimeContext(application.Id, workflowInstance.Id, entryInstance.Id, application.Status));
		application.RecordWorkflowStarted(workflowInstance.Id, entryInstance.Id);
		application.RecordStageEntered(workflowInstance.Id, entryInstance.Id);

		var formInstance = new AdmissionFormInstance(
			GuidGenerator.Create(), CurrentTenant.Id, application.Id, form.Id, form.Version);
		await PersistAsync(application, workflowInstance, stages, formInstance,
			await CreateInitialValuesAsync(applicant, formInstance, inquiry), inquiryId, workflow, entryInstance);
		await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().GetForApplicationAsync(application);
		return AdmissionApplicationMapper.ToDto(application);
	}

	private async Task<AdmissionApplication?> FindExistingAsync(Guid applicantId, Guid offeringId)
	{
		var query = await applicationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(application =>
			application.ApplicantId == applicantId && application.AdmissionOfferingId == offeringId &&
			application.Status != AdmissionApplicationStatus.Withdrawn));
	}

	private async Task<Guid?> ResolveInquiryAsync(Guid applicantId, Guid offeringId, Guid? inquiryId)
	{
		if (inquiryId.HasValue) return inquiryId;
		var query = await inquiryRepository.GetQueryableAsync();
		return (await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(inquiry => inquiry.ApplicantId == applicantId && inquiry.AdmissionOfferingId == offeringId)
			.OrderByDescending(inquiry => inquiry.CreationTime)))?.Id;
	}

	private async Task<AdmissionInquiry?> EnsureInquiryMatchesAsync(Guid? inquiryId, Guid applicantId, AdmissionOffering offering)
	{
		if (!inquiryId.HasValue) return null;
		var inquiry = await inquiryRepository.FindAsync(inquiryId.Value) ??
			throw new EntityNotFoundException(typeof(AdmissionInquiry), inquiryId.Value);
		if (inquiry.ApplicantId != applicantId || inquiry.AdmissionCycleId != offering.AdmissionCycleId ||
			inquiry.AdmissionOfferingId != offering.Id)
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
		return inquiry;
	}

	private async Task<AdmissionFormFieldValue[]> CreateInitialValuesAsync(
		AdmissionApplicant applicant,
		AdmissionFormInstance instance,
		AdmissionInquiry? inquiry)
	{
		var family = await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var fields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId);
		var inquiryValues = ReadInquiryValues(inquiry?.FormResponseSnapshotJson);
		var inquiryFields = inquiry?.FormResponseDefinitionId is Guid inquiryFormId
			? await fieldRepository.GetListAsync(field => field.FormDefinitionId == inquiryFormId)
			: [];
		return fields.Select(field => new AdmissionFormFieldValue(
				GuidGenerator.Create(), CurrentTenant.Id, instance.Id, field.Code,
				ResolveInitialValue(field, applicant, guardian, inquiryFields, inquiryValues)))
			.Where(value => value.ValueJson != "null" && !string.IsNullOrWhiteSpace(value.ValueJson)).ToArray();
	}

	private static string? ResolveInitialValue(
		AdmissionFormFieldDefinition field,
		AdmissionApplicant applicant,
		AdmissionGuardian? guardian,
		IReadOnlyCollection<AdmissionFormFieldDefinition> inquiryFields,
		IReadOnlyDictionary<string, string> inquiryValues)
	{
		if (field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey))
			return JsonSerializer.Serialize(AdmissionApplicationMapper.GetBoundValue(field.BindingKey, applicant, guardian));
		return AdmissionInquiryFormPrefill.ResolveCustomValue(field, inquiryFields, inquiryValues)
			?? field.DefaultValue;
	}

	private static IReadOnlyDictionary<string, string> ReadInquiryValues(string? snapshotJson)
	{
		if (string.IsNullOrWhiteSpace(snapshotJson))
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var values = JsonSerializer.Deserialize<Dictionary<string, string>>(snapshotJson) ?? [];
			return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
		}
		catch (JsonException)
		{
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private async Task PersistAsync(
		AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflowInstance,
		IEnumerable<AdmissionApplicationStageInstance> stages,
		AdmissionFormInstance formInstance,
		AdmissionFormFieldValue[] values,
		Guid? inquiryId,
		AdmissionWorkflowDefinition workflow,
		AdmissionApplicationStageInstance entry)
	{
		await applicationRepository.InsertAsync(application, false);
		await workflowInstanceRepository.InsertAsync(workflowInstance, false);
		foreach (var stage in stages) await stageInstanceRepository.InsertAsync(stage, false);
		await formInstanceRepository.InsertAsync(formInstance, false);
		foreach (var value in values) await valueRepository.InsertAsync(value, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, formInstance.Id,
			application.Id, "Created", null, CurrentUser.Id), false);
		if (inquiryId.HasValue && await inquiryRepository.FindAsync(inquiryId.Value) is { } inquiry)
		{
			inquiry.SetStatus(AdmissionInquiryStatus.ApplicationStarted);
			await inquiryRepository.UpdateAsync(inquiry, false);
		}
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"WorkflowStarted", $"Workflow {workflow.Name} v{workflow.Version} started", application.ApplicantId,
			application.Id, inquiryId), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"WorkflowStageEntered", $"Stage entered: {entry.Name}", application.ApplicantId,
			application.Id, inquiryId), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationStarted", "Application started", application.ApplicantId, application.Id, inquiryId), true);
	}

}
