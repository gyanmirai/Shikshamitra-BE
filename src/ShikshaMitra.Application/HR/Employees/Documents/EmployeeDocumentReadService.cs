using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
public class EmployeeDocumentReadService(
	IEmployeeRepository employeeRepository,
	IDocumentSettingRepository documentSettingRepository,
	IBlobContainer blobContainer)
	: ApplicationService, ITransientDependency
{
	public async Task<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>> GetChecklistAsync(Guid employeeId, int page, int pageSize, string? search)
	{
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await documentSettingRepository.GetQueryableAsync();
		if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(x => x.DocumentName.ToLower().Contains(term)); }
		var count = await AsyncExecuter.CountAsync(query);
		var definitions = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.DocumentName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize));
		var documentQuery = await GetDocumentQueryAsync(employeeId);
		var documentCount = await AsyncExecuter.CountAsync(documentQuery);
		if (documentCount > 500000) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		if (documentCount == 0) await EnsureEmployeeExistsAsync(employeeId);
		var definitionIds = definitions.Select(definition => definition.Id).ToArray();
		var documents = definitionIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(documentQuery
			.Where(document => document.DocumentNameId.HasValue && Enumerable.Contains(definitionIds, document.DocumentNameId.Value)));
		var names = definitions.ToDictionary(x => x.Id, x => x.DocumentName);
		var latest = documents.Where(x => x.DocumentNameId.HasValue).GroupBy(x => x.DocumentNameId!.Value).ToDictionary(x => x.Key, x => x.OrderByDescending(item => item.CreationTime).First());
		return new(count, definitions.Select(definition => {
			var uploaded = latest.GetValueOrDefault(definition.Id);
			return new EmployeeDocumentChecklistItemDto {
				Id = definition.Id, EmployeeId = employeeId, Title = definition.DocumentName, DocumentSize = definition.DocumentSize,
				DocumentTypes = definition.DocumentTypes.Select(x => (int)x).ToList(), DocType = string.Join(", ", definition.DocumentTypes),
				StatusVal = uploaded == null ? "Not Uploaded" : "Uploaded", UploadedDoc = uploaded == null ? null : EmployeeDocumentProjection.ToListItem(uploaded, names)
			};
		}).ToList());
	}
	public async Task<EmployeeDocumentListResponse> GetDocumentListAsync(Guid employeeId, int page, int pageSize, string? search)
	{
		const int defaultPageSize = 10;
		const int maxPageSize = 100;

		var normalizedPage = page < 1 ? 1 : page;
		var normalizedPageSize = pageSize < 1 ? defaultPageSize : Math.Min(pageSize, maxPageSize);

		var documents = await AsyncExecuter.ToListAsync(await GetDocumentQueryAsync(employeeId));
		if (documents.Count == 0) await EnsureEmployeeExistsAsync(employeeId);
		var settingsLookup = await GetSettingsLookupAsync(documents);
		var filtered = FilterDocuments(documents, settingsLookup, search);
		var totalCount = filtered.Count();
		var items = filtered
			.OrderByDescending(d => d.CreationTime)
			.Skip((normalizedPage - 1) * normalizedPageSize)
			.Take(normalizedPageSize)
			.Select(d => EmployeeDocumentProjection.ToListItem(d, settingsLookup))
			.ToList();

		var totalPages = totalCount == 0
			? 0
			: (int)Math.Ceiling((double)totalCount / normalizedPageSize);

		return new EmployeeDocumentListResponse
		{
			TotalCount = totalCount,
			CurrentPage = normalizedPage,
			PageSize = normalizedPageSize,
			TotalPages = totalPages,
			Items = items
		};
	}

	public async Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var document = await AsyncExecuter.FirstOrDefaultAsync((await GetDocumentQueryAsync(employeeId))
			.Where(item => item.Id == documentId));
		if (document == null)
		{
			await EnsureEmployeeExistsAsync(employeeId);
			throw new BusinessException(HRErrorCodes.EmployeeDocumentNotFound);
		}
		var content = await ReadBlobAsync(document.BlobName);

		return new DocumentDto { FileName = document.DocumentName, ContentType = document.ContentType, Content = content, Size = document.Size };
	}

	public async Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId)
	{
		var documents = await AsyncExecuter.ToListAsync((await GetDocumentQueryAsync(employeeId)).Select(document => new DocumentMetadataDto
		{
			Id = document.Id, FileName = document.DocumentName, Size = document.Size, ContentType = document.ContentType
		}));
		if (documents.Count == 0) await EnsureEmployeeExistsAsync(employeeId);
		return documents;
	}

	public async Task<byte[]> ReadBlobAsync(string blobName)
	{
		await using var stream = await blobContainer.GetAsync(blobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return memoryStream.ToArray();
	}

	private async Task<IQueryable<EmployeeDocumentReadItem>> GetDocumentQueryAsync(Guid employeeId)
		=> EmployeeDocumentReadItem.Query(await employeeRepository.GetQueryableAsync(), employeeId);

	private async Task EnsureEmployeeExistsAsync(Guid employeeId)
	{
		var employees = await employeeRepository.GetQueryableAsync();
		// Keep employee-not-found behavior and the parent's tenant/soft-delete filters;
		// mutation methods continue loading their aggregate through the domain manager.
		if (!await AsyncExecuter.AnyAsync(employees.Where(employee => employee.Id == employeeId)))
			throw new EntityNotFoundException(typeof(Employee), employeeId);
	}

	private async Task<Dictionary<Guid, string>> GetSettingsLookupAsync(IEnumerable<EmployeeDocumentReadItem> documents)
	{
		var nameIds = documents
			.Where(d => d.DocumentNameId.HasValue)
			.Select(d => d.DocumentNameId!.Value)
			.Distinct()
			.ToList();

		if (nameIds.Count == 0)
		{
			return [];
		}

		var settings = await documentSettingRepository.GetListAsync(s => nameIds.Contains(s.Id));
		return settings.ToDictionary(s => s.Id, s => s.DocumentName);
	}

	private static IEnumerable<EmployeeDocumentReadItem> FilterDocuments(
		IEnumerable<EmployeeDocumentReadItem> documents,
		IReadOnlyDictionary<Guid, string> settingsLookup,
		string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return documents;
		}

		return documents.Where(d =>
			d.DocumentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
			(d.DocumentNameId.HasValue && settingsLookup.TryGetValue(d.DocumentNameId.Value, out var name) &&
			 name.Contains(search, StringComparison.OrdinalIgnoreCase)));
	}
}
