using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationNumberSeriesWorkflow(
	IRepository<AdmissionCrmFormNumberSeries, Guid> repository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmRegistrationAccess access) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetListAsync(Guid yearId)
	{
		await access.EnsureYearAsync(yearId, "GetFormNumberSeriesAsync");
		var queryable = await repository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		return new PagedResultDto<AdmissionCrmFormNumberSeriesDto>(items.Count,
			items.OrderBy(x => x.Name).Select(AdmissionCrmRegistrationModelMapper.ToSeriesDto).ToList());
	}

	public async Task<AdmissionCrmFormNumberSeriesDto> CreateAsync(Guid yearId, AdmissionCrmFormNumberSeriesUpsertDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(yearId, "CreateFormNumberSeriesAsync");
		var series = new AdmissionCrmFormNumberSeries(GuidGenerator.Create(), CurrentTenant.Id, yearId, input.Name, input.Prefix,
			input.Suffix, input.Padding, input.NextNumber, AdmissionCrmRegistrationModelMapper.Serialize(input.Applicability), input.IsActive);
		await repository.InsertAsync(series, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToSeriesDto(series);
	}

	public async Task<AdmissionCrmFormNumberSeriesDto> UpdateAsync(Guid id, AdmissionCrmFormNumberSeriesUpsertDto input)
	{
		var series = await access.GetSeriesAsync(id, "UpdateFormNumberSeriesAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(series.AcademicYearId, "UpdateFormNumberSeriesAsync");
		series.Update(input.Name, input.Prefix, input.Suffix, input.Padding, input.NextNumber,
			AdmissionCrmRegistrationModelMapper.Serialize(input.Applicability), input.IsActive);
		await repository.UpdateAsync(series, autoSave: true);
		return AdmissionCrmRegistrationModelMapper.ToSeriesDto(series);
	}
}
