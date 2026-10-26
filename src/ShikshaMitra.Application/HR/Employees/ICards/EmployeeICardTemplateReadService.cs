using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardTemplateReadService(
	IRepository<EmployeeICardTemplate, Guid> repository,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	private const int MaxPageSize = 100;

	public async Task<EmployeeICardTemplateListResponse> GetListAsync(
		Guid? academicYearId = null,
		Guid? mediumStageId = null,
		int page = 1,
		int pageSize = 20,
		string? search = null)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

		var query = await repository.GetQueryableAsync();
		query = query
			.WhereIf(academicYearId.HasValue, x => x.AcademicYearId == academicYearId)
			.WhereIf(mediumStageId.HasValue, x => x.MediumStageId == mediumStageId)
			.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!));

		var totalCount = await asyncExecuter.CountAsync(query);
		var items = await asyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new EmployeeICardTemplateListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			Items = items.Select(EmployeeICardTemplateProjection.ToDto).ToList()
		};
	}

	public async Task<EmployeeICardTemplateDto> GetAsync(Guid id) =>
		EmployeeICardTemplateProjection.ToDto(await repository.GetAsync(id));
}
