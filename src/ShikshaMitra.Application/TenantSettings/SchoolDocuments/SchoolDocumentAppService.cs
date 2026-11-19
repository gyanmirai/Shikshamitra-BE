using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

[Authorize]
[RemoteService(false)]
public partial class SchoolDocumentAppService(
	IRepository<SchoolDocument, Guid> documents,
	IRepository<SchoolDocumentFile, Guid> files,
	IBlobContainer blobs) : ApplicationService, ISchoolDocumentAppService
{
	public async Task<PagedResultDto<SchoolDocumentDto>> GetListAsync(SchoolDocumentListInput input)
	{
		await AuthorizationService.CheckAsync(SchoolDocumentPermissions.View);
		var tenantId = RequireTenant();
		if (input.Category.HasValue && !Enum.IsDefined(input.Category.Value))
			throw new BusinessException(SchoolDocumentConsts.InvalidDetails);
		var query = (await documents.GetQueryableAsync()).Where(x => x.TenantId == tenantId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}
		if (input.Category.HasValue) query = query.Where(x => x.Category == input.Category.Value);
		var total = await AsyncExecuter.LongCountAsync(query);
		var sorted = input.Sorting?.Trim().ToLowerInvariant() switch
		{
			"name asc" => query.OrderBy(x => x.Name),
			"name desc" => query.OrderByDescending(x => x.Name),
			"creationtime asc" => query.OrderBy(x => x.CreationTime),
			"creationtime desc" or "" or null => query.OrderByDescending(x => x.CreationTime),
			_ => throw new BusinessException(SchoolDocumentConsts.InvalidDetails)
		};
		var rows = await AsyncExecuter.ToListAsync(sorted.ThenBy(x => x.Id)
			.Skip(Math.Max(0, input.SkipCount)).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var ids = rows.Select(x => x.Id).ToArray();
		var attachments = ids.Length == 0 ? [] : await files.GetListAsync(x =>
			x.TenantId == tenantId && Enumerable.Contains(ids, x.DocumentId));
		var lookup = attachments.ToLookup(x => x.DocumentId);
		return new(total, rows.Select(row => Map(row, lookup[row.Id])).ToList());
	}

	public async Task<SchoolDocumentDto> GetAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(SchoolDocumentPermissions.View);
		var document = await FindDocumentAsync(id);
		return Map(document, await FilesAsync(id));
	}

	public async Task<SchoolDocumentDownloadDto> GetFileAsync(Guid id, Guid fileId)
	{
		await AuthorizationService.CheckAsync(SchoolDocumentPermissions.View);
		await FindDocumentAsync(id);
		var tenantId = RequireTenant();
		var file = await files.FindAsync(x => x.Id == fileId && x.DocumentId == id && x.TenantId == tenantId)
			?? throw new EntityNotFoundException(typeof(SchoolDocumentFile), fileId);
		var content = await blobs.GetAllBytesOrNullAsync(file.BlobName)
			?? throw new EntityNotFoundException(typeof(SchoolDocumentFile), fileId);
		return new() { Id = file.Id, FileName = file.FileName, ContentType = file.ContentType, Size = file.Size, Content = content };
	}

	private Guid RequireTenant() => CurrentTenant.Id ?? throw new AbpAuthorizationException();

	private async Task<SchoolDocument> FindDocumentAsync(Guid id)
	{
		var tenantId = RequireTenant();
		return await documents.FindAsync(x => x.Id == id && x.TenantId == tenantId)
			?? throw new EntityNotFoundException(typeof(SchoolDocument), id);
	}

	private Task<List<SchoolDocumentFile>> FilesAsync(Guid id)
	{
		var tenantId = RequireTenant();
		return files.GetListAsync(x => x.DocumentId == id && x.TenantId == tenantId);
	}

	private static SchoolDocumentDto Map(SchoolDocument document, IEnumerable<SchoolDocumentFile> attachments) => new()
	{
		Id = document.Id, Name = document.Name, Category = document.Category, Notes = document.Notes,
		UploadedBy = document.UploadedBy, CreationTime = document.CreationTime, ConcurrencyStamp = document.ConcurrencyStamp,
		Files = attachments.OrderBy(x => x.CreationTime).ThenBy(x => x.Id).Select(x => new SchoolDocumentFileDto
			{ Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, Size = x.Size }).ToList()
	};
}
