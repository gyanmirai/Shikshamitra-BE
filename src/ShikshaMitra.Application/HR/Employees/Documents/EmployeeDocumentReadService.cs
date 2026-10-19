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
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
public class EmployeeDocumentReadService(
	EmployeeDocumentManager employeeDocumentManager,
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
		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);
		if (documents.Count > 500000) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
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

		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);
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
		var document = await employeeDocumentManager.GetDocumentAsync(employeeId, documentId);
		var content = await ReadBlobAsync(document.BlobName);

		return EmployeeDocumentProjection.ToDocumentDto(document, content);
	}

	public async Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId)
	{
		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);
		return documents.Select(EmployeeDocumentProjection.ToMetadata).ToList();
	}

	public async Task<byte[]> ReadBlobAsync(string blobName)
	{
		await using var stream = await blobContainer.GetAsync(blobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return memoryStream.ToArray();
	}

	private async Task<Dictionary<Guid, string>> GetSettingsLookupAsync(IEnumerable<EmployeeDocument> documents)
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

	private static IEnumerable<EmployeeDocument> FilterDocuments(
		IEnumerable<EmployeeDocument> documents,
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
