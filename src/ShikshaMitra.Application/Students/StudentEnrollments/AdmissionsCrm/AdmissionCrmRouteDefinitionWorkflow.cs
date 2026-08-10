using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteDefinitionWorkflow(
	IRepository<AdmissionCrmRouteDefinition, Guid> definitionRepository,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRouteDefinitionDto> CreateAsync(
		Guid academicYearId,
		CreateAdmissionCrmRouteDefinitionDto input)
	{
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, nameof(CreateAsync));
		var queryable = await definitionRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id &&
			entity.AcademicYearId == academicYearId &&
			entity.Code == input.Code.ToUpper()));
		var version = existing.Count == 0 ? 1 : existing.Max(entity => entity.Version) + 1;
		var entity = new AdmissionCrmRouteDefinition(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.Code,
			input.Name,
			input.RouteType,
			version,
			AdmissionCrmRouteMapper.Serialize(input.Configuration),
			input.EffectiveFrom,
			input.EffectiveUntil);
		await definitionRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetListAsync(Guid academicYearId)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetListAsync));
		var queryable = await definitionRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.AcademicYearId == academicYearId));
		return entities.OrderBy(entity => entity.RouteType)
			.ThenByDescending(entity => entity.Version)
			.Select(AdmissionCrmRouteMapper.Map)
			.ToList();
	}

	public async Task<AdmissionCrmRouteDefinitionDto> SubmitAsync(Guid id)
	{
		var entity = await GetAsync(id);
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(entity.AcademicYearId, nameof(SubmitAsync));
		entity.Submit(GetCurrentUserId());
		await definitionRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<AdmissionCrmRouteDefinitionDto> PublishAsync(Guid id)
	{
		var entity = await GetAsync(id);
		await studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(entity.AcademicYearId, nameof(PublishAsync));
		entity.Publish(GetCurrentUserId());
		var queryable = await definitionRepository.GetQueryableAsync();
		var prior = await AsyncExecuter.ToListAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id &&
			item.AcademicYearId == entity.AcademicYearId &&
			item.Code == entity.Code &&
			item.Id != entity.Id &&
			item.Status == AdmissionCrmRouteDefinitionStatus.Published));
		foreach (var item in prior)
		{
			item.Retire();
			await definitionRepository.UpdateAsync(item);
		}

		await definitionRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<AdmissionCrmRouteDefinition> GetAsync(Guid id)
	{
		var entity = await definitionRepository.GetAsync(id);
		if (entity.TenantId != CurrentTenant.Id)
		{
			throw new AbpAuthorizationException("Route definition belongs to another tenant.");
		}

		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(GetAsync));
		return entity;
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
