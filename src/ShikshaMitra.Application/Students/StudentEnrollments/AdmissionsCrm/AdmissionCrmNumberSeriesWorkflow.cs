using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmNumberSeriesWorkflow(
	IRepository<AdmissionCrmNumberSeries, Guid> seriesRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmActivationAccess access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmNumberSeriesDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmNumberSeriesDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, "CreateNumberSeriesAsync");
		var query = await seriesRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
		    x.AcademicYearId == academicYearId && x.Code == input.Code.ToUpper())))
			throw new BusinessException("AdmissionCrm:NumberSeriesCodeAlreadyExists");
		var entity = new AdmissionCrmNumberSeries(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.IdentifierType, input.EducationBoardId, input.AcademicMediumId,
			input.StandardId, input.Prefix, input.Suffix, input.Padding, input.StartingSequence,
			input.MaximumSequence, input.EffectiveFrom, input.EffectiveUntil);
		await seriesRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmActivationMapper.Series(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmNumberSeriesDto>> GetListAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, "GetNumberSeriesAsync");
		var query = await seriesRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderBy(x => x.IdentifierType).ThenBy(x => x.Code).Select(AdmissionCrmActivationMapper.Series).ToList();
	}

	public async Task<AdmissionCrmNumberSeriesDto> SubmitAsync(Guid id)
	{
		var entity = await access.GetSeriesAsync(id);
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(entity.AcademicYearId, "SubmitNumberSeriesAsync");
		entity.Submit(GetUserId());
		await seriesRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmActivationMapper.Series(entity);
	}

	public async Task<AdmissionCrmNumberSeriesDto> PublishAsync(Guid id)
	{
		var entity = await access.GetSeriesAsync(id);
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(entity.AcademicYearId, "PublishNumberSeriesAsync");
		var query = await seriesRepository.GetQueryableAsync();
		var overlapping = await AsyncExecuter.AnyAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == entity.AcademicYearId && x.Id != entity.Id && x.IdentifierType == entity.IdentifierType &&
			x.EducationBoardId == entity.EducationBoardId && x.AcademicMediumId == entity.AcademicMediumId &&
			x.StandardId == entity.StandardId && x.Status == AdmissionCrmNumberSeriesStatus.Published &&
			(!x.EffectiveUntil.HasValue || x.EffectiveUntil >= entity.EffectiveFrom) &&
			(!entity.EffectiveUntil.HasValue || x.EffectiveFrom <= entity.EffectiveUntil)));
		if (overlapping) throw new BusinessException("AdmissionCrm:OverlappingPublishedNumberSeries");
		entity.Publish(GetUserId());
		await seriesRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmActivationMapper.Series(entity);
	}

	private Guid GetUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
