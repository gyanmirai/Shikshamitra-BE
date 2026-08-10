using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationAccess(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionInquiryFormTemplate, Guid> formRepository,
	IRepository<AdmissionCrmMessageTemplate, Guid> messageRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	StudentScopeAccessService scopeAccess,
	IUserScopeAccessService userScopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmCycleConfiguration> EnsureCycleAsync(Guid yearId)
	{
		await EnsureYearExistsAsync(yearId);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "EnsureCycleConfigurationAsync");
		var queryable = await cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		if (existing != null) return existing;
		var configuration = new AdmissionCrmCycleConfiguration(GuidGenerator.Create(), CurrentTenant.Id, yearId, "Default Admission Year");
		var schoolDefault = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == Guid.Empty));
		if (schoolDefault != null) configuration.CopySettingsFrom(schoolDefault, schoolDefault.CycleName);
		await cycleRepository.InsertAsync(configuration, autoSave: true);
		return configuration;
	}

	public async Task<AdmissionCrmCycleConfiguration> EnsureSchoolDefaultCycleAsync()
	{
		var queryable = await cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == Guid.Empty));
		if (existing != null) return existing;
		var configuration = new AdmissionCrmCycleConfiguration(GuidGenerator.Create(), CurrentTenant.Id,
			Guid.Empty, "School Admissions CRM Defaults");
		await cycleRepository.InsertAsync(configuration, autoSave: true);
		return configuration;
	}

	public async Task<IReadOnlyList<AdmissionCrmPipelineStage>> EnsurePipelineStagesAsync(Guid yearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "EnsurePipelineStagesAsync");
		var queryable = await stageRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		if (existing.Count > 0) return existing;
		var defaults = AdmissionCrmConfigurationMapper.DefaultPipelineStages().Select(x => new AdmissionCrmPipelineStage(
			GuidGenerator.Create(), CurrentTenant.Id, yearId, x.SystemStage, x.StageName, x.DisplayOrder, x.ColorHex,
			x.SlaTargetHours, x.OwnerResponsibility, AdmissionCrmConfigurationMapper.SerializeAllowedNextStages(x.AllowedNextStages))).ToList();
		foreach (var stage in defaults) await stageRepository.InsertAsync(stage, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return defaults;
	}

	public async Task<AdmissionCrmPipelineStage> GetStageAsync(Guid id, string operation)
	{
		var stage = await stageRepository.GetAsync(id);
		if (stage.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:PipelineStageNotFound").WithData("StageId", id);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(stage.AcademicYearId, operation);
		return stage;
	}

	public async Task<AdmissionInquiryFormTemplate> GetFormAsync(Guid id, string operation)
	{
		var template = await formRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:FormTemplateNotFound").WithData("TemplateId", id);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		return template;
	}

	public async Task<AdmissionCrmMessageTemplate> GetMessageAsync(Guid id, string operation)
	{
		var template = await messageRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:MessageTemplateNotFound").WithData("TemplateId", id);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(template.AcademicYearId, operation);
		return template;
	}

	public async Task EnsureYearMutationAsync(Guid yearId, string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, operation);
		if (!await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(yearId))
			throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired").WithData("AcademicYearId", yearId).WithData("Operation", operation);
	}

	public async Task EnsureTenantMutationAsync(string operation)
	{
		if (!await userScopeAccess.CurrentUserHasTenantWideScopeAsync())
			throw new BusinessException("AdmissionCrm:TenantWideScopeRequired").WithData("Operation", operation);
	}

	private async Task EnsureYearExistsAsync(Guid yearId)
	{
		var queryable = await academicYearRepository.GetQueryableAsync();
		if (!await AsyncExecuter.AnyAsync(queryable.Where(x => x.Id == yearId && x.TenantId == CurrentTenant.Id)))
			throw new BusinessException("AdmissionCrm:AcademicYearNotFound").WithData("AcademicYearId", yearId);
	}
}
