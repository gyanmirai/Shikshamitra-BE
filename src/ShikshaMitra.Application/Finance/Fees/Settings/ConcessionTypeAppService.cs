using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Setting.ConcessionType.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

[Authorize]
[RemoteService(false)]
public class ConcessionTypeAppService(
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IConcessionTypeAppService
{
	public async Task CreateAsync(Guid academicYearId, ConcessionTypeRequest concessionTypeRequest)
	{
		var academicYear = await academicYearRepository.GetAsync(academicYearId) ?? throw new UserFriendlyException("Academic Year not found.");
		var concessionType = new ConcessionType(
			 guidGenerator.Create(),
			 CurrentTenant.Id,
			 academicYearId,
			 concessionTypeRequest.Name,
			 concessionTypeRequest.Description
		);

		foreach (var document in concessionTypeRequest.Documents)
		{
			concessionType.AddDocument(document.DocumentName, document.DocumentSize, document.AcceptedFormat);
		}

		await concessionTypeRepository.InsertAsync(concessionType);
	}

	public async Task<PagedResultDto<ConcessionTypeResponse>> GetListAsync(Guid academicYearId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null)
	{
		var query = await concessionTypeRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId && (string.IsNullOrEmpty(searchTerm) || x.Name.Contains(searchTerm)));
		var totalCount = query.Count();
		var result = await AsyncExecuter.ToListAsync(
			query.Skip(skipCount).Take(maxResultCount)
		);

		return new PagedResultDto<ConcessionTypeResponse>
		{
			TotalCount = totalCount,
			Items = result.Select(x => new ConcessionTypeResponse
			{
				Id = x.Id,
				Name = x.Name,
				Description = x.Description,
				Documents = x.ConcessionTypeDocuments.Select(d => new ConcessionTypeDocumentDto
				{
					Id = d.Id,
					DocumentName = d.DocumentName,
					DocumentSize = d.DocumentSize,
					AcceptedFormat = d.AcceptedFormats.Select(f => f.DocumentType).ToList(),
				}).ToList()
			}).ToList()
		};
	}

	public async Task<ConcessionTypeResponse> GetAsync(Guid id)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		if (concessionType == null)
		{
			throw new UserFriendlyException("Concession Type not found.");
		}
		return new ConcessionTypeResponse
		{
			Id = concessionType.Id,
			Name = concessionType.Name,
			Documents = concessionType.ConcessionTypeDocuments.Select(d => new ConcessionTypeDocumentDto
			{
				Id = d.Id,
				DocumentName = d.DocumentName,
				DocumentSize = d.DocumentSize,
				AcceptedFormat = d.AcceptedFormats.Select(f => f.DocumentType).ToList(),
			}).ToList(),
			Description = concessionType.Description
		};
	}

	public async Task UpdateAsync(Guid id, ConcessionTypeRequest input)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id) ?? throw new UserFriendlyException("Concession Type not found.");
		concessionType.Update(input.Name, input.Description);
		concessionType.ReplaceDocuments(input.Documents.Select(d => (d.DocumentName, d.DocumentSize, d.AcceptedFormat)).ToList());
		await concessionTypeRepository.UpdateAsync(concessionType);
	}

	public async Task DeleteAsync(Guid id)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id) ?? throw new UserFriendlyException("Concession Type not found.");
		await concessionTypeRepository.DeleteAsync(concessionType);
	}
}
