using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingCommandService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	AdmissionOfferingConfigurationValidator configurationValidator,
	AdmissionOfferingReadService readService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferingDto> CreateAsync(Guid admissionCycleId, CreateAdmissionOfferingDto input)
	{
		var validation = await configurationValidator.ValidateAsync(admissionCycleId, input);
		AdmissionOfferingConfigurationValidator.EnsureCanSave(validation, input.IsActive);
		var offering = CreateEntity(admissionCycleId, input);
		await offeringRepository.InsertAsync(offering, autoSave: true);
		return await readService.GetAsync(offering.Id);
	}

	public async Task<AdmissionOfferingDto> UpdateAsync(Guid id, UpdateAdmissionOfferingDto input)
	{
		var offering = await GetOfferingAsync(id);
		EnsureConcurrency(offering, input.ConcurrencyStamp);
		if (offering.IsActive != input.IsActive)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingStateCommandRequired,
				"Use the Activate or Deactivate action to change Admission Offering status.");
		}

		var validation = await configurationValidator.ValidateAsync(offering.AdmissionCycleId, input, offering.Id);
		AdmissionOfferingConfigurationValidator.EnsureCanSave(validation, offering.IsActive);
		var hasApplications = await applicationRepository.AnyAsync(application => application.AdmissionOfferingId == offering.Id);
		offering.UpdateIdentity(input.CampusId, input.BoardId, input.MediumId, input.StageId, input.StandardId, hasApplications);
		offering.UpdateOperationalDetails(input.Code, input.UseCycleDates, input.OpenDate, input.CloseDate);
		offering.ConfigureReferences(input.WorkflowDefinitionId, input.InquiryFormDefinitionId, input.ApplicationFormDefinitionId);
		await offeringRepository.UpdateAsync(offering, autoSave: true);
		return await readService.GetAsync(offering.Id);
	}

	public async Task<AdmissionOfferingDto> ActivateAsync(Guid id, string concurrencyStamp)
	{
		var offering = await GetOfferingAsync(id);
		EnsureConcurrency(offering, concurrencyStamp);
		var validation = await configurationValidator.ValidateAsync(
			offering.AdmissionCycleId, ToWriteDto(offering), offering.Id);
		AdmissionOfferingConfigurationValidator.EnsureCanSave(validation, isActive: true);
		offering.Activate();
		await offeringRepository.UpdateAsync(offering, autoSave: true);
		return await readService.GetAsync(offering.Id);
	}

	public async Task<AdmissionOfferingDto> DeactivateAsync(Guid id, string concurrencyStamp)
	{
		var offering = await GetOfferingAsync(id);
		EnsureConcurrency(offering, concurrencyStamp);
		offering.Deactivate();
		await offeringRepository.UpdateAsync(offering, autoSave: true);
		return await readService.GetAsync(offering.Id);
	}

	public async Task DeleteAsync(Guid id, string concurrencyStamp)
	{
		var offering = await GetOfferingAsync(id);
		EnsureConcurrency(offering, concurrencyStamp);
		if (await applicationRepository.AnyAsync(application => application.AdmissionOfferingId == offering.Id))
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingHasApplications,
				"An Admission Offering with Applications cannot be deleted. Deactivate it instead.");
		}
		await offeringRepository.DeleteAsync(offering, autoSave: true);
	}

	public async Task<AdmissionOfferingDto> CloneAsync(Guid id, CloneAdmissionOfferingDto input)
	{
		var source = await GetOfferingAsync(id);
		var cloneInput = new CreateAdmissionOfferingDto
		{
			CampusId = input.CampusId,
			BoardId = input.BoardId,
			MediumId = input.MediumId,
			StageId = input.StageId,
			StandardId = input.StandardId,
			Code = input.Code,
			UseCycleDates = !source.OpenDate.HasValue && !source.CloseDate.HasValue,
			OpenDate = source.OpenDate,
			CloseDate = source.CloseDate,
			WorkflowDefinitionId = source.WorkflowDefinitionId,
			InquiryFormDefinitionId = source.InquiryFormDefinitionId,
			ApplicationFormDefinitionId = source.ApplicationFormDefinitionId,
			IsActive = input.IsActive ?? source.IsActive
		};
		return await CreateAsync(input.AdmissionCycleId, cloneInput);
	}

	public async Task<AdmissionOfferingConfigurationValidationDto> ValidateAsync(ValidateAdmissionOfferingDto input)
	{
		var validation = await configurationValidator.ValidateAsync(
			input.AdmissionCycleId, input, input.ExcludingOfferingId);
		return AdmissionOfferingReadService.MapValidation(validation);
	}

	public async Task<BulkAdmissionOfferingResultDto> BulkCreateAsync(
		Guid admissionCycleId,
		BulkCreateAdmissionOfferingsDto input)
	{
		var result = new BulkAdmissionOfferingResultDto { IsPreview = input.PreviewOnly };
		var pending = new List<(BulkAdmissionOfferingItemDto Item, CreateAdmissionOfferingDto Input)>();
		foreach (var item in input.Items)
		{
			var createInput = ToCreateDto(input, item);
			var validation = await configurationValidator.ValidateAsync(admissionCycleId, createInput);
			result.Items.Add(new BulkAdmissionOfferingResultItemDto
			{
				StandardId = item.StandardId,
				Code = item.Code,
				IsValid = validation.IsValid,
				Errors = AdmissionOfferingReadService.MapValidation(validation).Errors
			});
			pending.Add((item, createInput));
		}

		result.CanCommit = result.Items.All(item => item.IsValid);
		if (input.PreviewOnly || !result.CanCommit)
		{
			return result;
		}

		for (var index = 0; index < pending.Count; index++)
		{
			var offering = CreateEntity(admissionCycleId, pending[index].Input);
			await offeringRepository.InsertAsync(offering, autoSave: false);
			result.Items[index].OfferingId = offering.Id;
		}
		await CurrentUnitOfWork.SaveChangesAsync();
		result.CreatedCount = pending.Count;
		return result;
	}

	private AdmissionOffering CreateEntity(Guid admissionCycleId, AdmissionOfferingWriteDto input)
	{
		var offering = new AdmissionOffering(
			GuidGenerator.Create(), CurrentTenant.Id, admissionCycleId, input.CampusId,
			input.BoardId, input.MediumId, input.StageId, input.StandardId, input.Code,
			input.UseCycleDates, input.OpenDate, input.CloseDate);
		offering.ConfigureReferences(
			input.WorkflowDefinitionId, input.InquiryFormDefinitionId, input.ApplicationFormDefinitionId);
		if (input.IsActive)
			offering.Activate();
		else
			offering.Deactivate();
		return offering;
	}

	private async Task<AdmissionOffering> GetOfferingAsync(Guid id) =>
		await offeringRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionOffering), id);

	private static void EnsureConcurrency(AdmissionOffering offering, string concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp) ||
			!string.Equals(offering.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingConcurrencyConflict,
				"This Admission Offering was modified by another user. Refresh to view the latest version.");
		}
	}

	private static CreateAdmissionOfferingDto ToWriteDto(AdmissionOffering offering) => new()
	{
		CampusId = offering.CampusId,
		BoardId = offering.BoardId,
		MediumId = offering.MediumId,
		StageId = offering.StageId,
		StandardId = offering.StandardId,
		Code = offering.Code,
		UseCycleDates = !offering.OpenDate.HasValue && !offering.CloseDate.HasValue,
		OpenDate = offering.OpenDate,
		CloseDate = offering.CloseDate,
		WorkflowDefinitionId = offering.WorkflowDefinitionId,
		InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
		ApplicationFormDefinitionId = offering.ApplicationFormDefinitionId,
		IsActive = offering.IsActive
	};

	private static CreateAdmissionOfferingDto ToCreateDto(
		BulkCreateAdmissionOfferingsDto input,
		BulkAdmissionOfferingItemDto item) => new()
	{
		CampusId = input.CampusId,
		BoardId = input.BoardId,
		MediumId = input.MediumId,
		StageId = input.StageId,
		StandardId = item.StandardId,
		Code = item.Code,
		UseCycleDates = input.UseCycleDates,
		OpenDate = input.OpenDate,
		CloseDate = input.CloseDate,
		WorkflowDefinitionId = input.WorkflowDefinitionId,
		InquiryFormDefinitionId = input.InquiryFormDefinitionId,
		ApplicationFormDefinitionId = input.ApplicationFormDefinitionId,
		IsActive = input.IsActive
	};
}
