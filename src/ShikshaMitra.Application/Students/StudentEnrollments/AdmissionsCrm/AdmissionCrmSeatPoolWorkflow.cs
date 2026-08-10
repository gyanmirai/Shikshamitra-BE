using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatPoolWorkflow(
	IRepository<AdmissionCrmSeatPool, Guid> repository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmSeatPoolDefinitionWorkflow definitions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSeatPoolDto> CreateAsync(Guid yearId, UpsertAdmissionCrmSeatPoolDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(yearId, "CreateSeatPoolAsync");
		var queryable = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == yearId && x.Code == input.Code.Trim())))
			throw new BusinessException("AdmissionCrm:SeatPoolCodeAlreadyExists");
		var definition = await definitions.ResolveAsync(yearId, input);
		var pool = new AdmissionCrmSeatPool(GuidGenerator.Create(), CurrentTenant.Id, yearId, input.PlaybookId,
			input.Code.Trim(), input.Name, definition.RoundCode, definition.Campus, definition.DesiredClass,
			definition.Board, definition.Medium, definition.ApplicantCategory, definition.OperationalTarget, definition.ApprovedCapacity);
		if (!input.IsActive) pool.UpdateDefinition(input.PlaybookId, input.Name, definition.RoundCode, definition.Campus,
			definition.DesiredClass, definition.Board, definition.Medium, definition.ApplicantCategory,
			definition.OperationalTarget, definition.ApprovedCapacity, false);
		await repository.InsertAsync(pool, autoSave: true);
		return AdmissionCrmFinalAdmissionMapper.SeatPool(pool);
	}

	public async Task<AdmissionCrmSeatPoolDto> UpdateAsync(Guid id, UpsertAdmissionCrmSeatPoolDto input)
	{
		var pool = await access.GetPoolAsync(id, "UpdateSeatPoolAsync");
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(pool.AcademicYearId, "UpdateSeatPoolAsync");
		if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp) && !string.Equals(pool.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException("AdmissionCrm:SeatPoolConcurrencyConflict");
		if (!string.Equals(pool.Code, input.Code.Trim(), StringComparison.Ordinal)) throw new BusinessException("AdmissionCrm:SeatPoolCodeIsImmutable");
		var definition = await definitions.ResolveAsync(pool.AcademicYearId, input);
		pool.UpdateDefinition(input.PlaybookId, input.Name, definition.RoundCode, definition.Campus, definition.DesiredClass,
			definition.Board, definition.Medium, definition.ApplicantCategory, definition.OperationalTarget,
			definition.ApprovedCapacity, input.IsActive);
		await repository.UpdateAsync(pool, autoSave: true);
		return AdmissionCrmFinalAdmissionMapper.SeatPool(pool);
	}

	public async Task<PagedResultDto<AdmissionCrmSeatPoolDto>> GetListAsync(Guid yearId, GetAdmissionCrmSeatPoolListInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "GetSeatPoolsAsync");
		var queryable = await repository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var filter = input.Filter.Trim().ToLower();
			query = query.Where(x => x.Code.ToLower().Contains(filter) || x.Name.ToLower().Contains(filter));
		}
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
		var count = await AsyncExecuter.CountAsync(query);
		var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Code).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmSeatPoolDto>(count, entities.Select(AdmissionCrmFinalAdmissionMapper.SeatPool).ToList());
	}
}
