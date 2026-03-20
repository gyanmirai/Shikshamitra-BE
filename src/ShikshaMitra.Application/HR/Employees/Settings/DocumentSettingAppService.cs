using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.Settings;

[RemoteService(false)]
public class DocumentSettingAppService(
	IDocumentSettingRepository repository,
	IGuidGenerator guidGenerator,
	ILogger<DocumentSettingAppService> logger)
	: CrudAppService<DocumentSetting, DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
			CreateUpdateDocumentSettingDto>(repository),
		IDocumentSettingAppService
{
	public async override Task<DocumentSettingDto> CreateAsync(CreateUpdateDocumentSettingDto input)
	{
		try
		{
			var exists = await repository.FindAsync(x => x.DocumentName == input.DocumentName);
			if (exists != null)
			{
				throw new UserFriendlyException($"Document setting with name '{input.DocumentName}' already exists.");
			}
			return await base.CreateAsync(input);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error creating document setting");
			throw;
		}
	}

	/// <summary>
	/// Get all document configurations (matches OpenAPI contract)
	/// </summary>
	public async Task<DocumentListResponse> GetDocumentListAsync(int page, int pageSize)
	{
		var queryable = await Repository.GetQueryableAsync();
		var skipCount = (page - 1) * pageSize;

		// Get total count for pagination
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		var items = await AsyncExecuter.ToListAsync(
			queryable.OrderBy(x => x.DocumentName)
				.Skip(skipCount)
				.Take(pageSize)
		);

		return new DocumentListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
			Items = items.Select(doc => new DocumentItem
			{
				Id = doc.Id,
				Title = doc.DocumentName,
				DocumentSize = (int)doc.DocumentSize,
				DocumentTypes = doc.DocumentTypes.Select(dt => (int)dt).ToList()
			}).ToList()
		};
	}

	/// <summary>
	/// Get document configuration by ID (matches OpenAPI contract)
	/// </summary>
	public async Task<DocumentItem> GetDocumentByIdAsync(Guid documentId)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var doc = await Repository.GetAsync(documentId);

		return new DocumentItem
		{
			Id = doc.Id,
			Title = doc.DocumentName,
			DocumentSize = (int)doc.DocumentSize,
			DocumentTypes = doc.DocumentTypes.Select(dt => (int)dt).ToList()
		};
	}

	/// <summary>
	/// Create document configuration (matches OpenAPI contract)
	/// </summary>
	public async Task<DocumentCreateResponse> CreateDocumentAsync(DocumentRequest input)
	{
		// Check for duplicate
		var exists = await repository.FindAsync(x => x.DocumentName == input.Title);
		if (exists != null)
		{
			throw new UserFriendlyException($"Document setting with name '{input.Title}' already exists.");
		}

		var documentTypes = input.DocumentTypes
			.Select(dt => (DocumentType)dt)
			.ToList();

		var doc = new DocumentSetting(
			guidGenerator.Create(),
			CurrentTenant.Id,
			input.Title,
			input.DocumentSize,
			isRequired: false,
			documentTypes
		);

		await Repository.InsertAsync(doc, autoSave: true);

		return new DocumentCreateResponse
		{
			Id = doc.Id
		};
	}

	/// <summary>
	/// Update document configuration (matches OpenAPI contract)
	/// </summary>
	public async Task UpdateDocumentAsync(Guid documentId, DocumentRequest input)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var doc = await Repository.GetAsync(documentId);

		// Check for duplicate name (excluding current document)
		var duplicate = await repository.FindAsync(x => x.DocumentName == input.Title && x.Id != documentId);
		if (duplicate != null)
		{
			throw new UserFriendlyException($"Document setting with name '{input.Title}' already exists.");
		}

		var documentTypes = input.DocumentTypes
			.Select(dt => (DocumentType)dt)
			.ToList();

		doc.Update(input.Title, input.DocumentSize, doc.IsRequired, documentTypes);
		await Repository.UpdateAsync(doc, autoSave: true);
	}

	/// <summary>
	/// Delete document configuration (matches OpenAPI contract)
	/// </summary>
	public async Task DeleteDocumentAsync(Guid documentId)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var doc = await Repository.GetAsync(documentId);
		await Repository.DeleteAsync(doc, autoSave: true);
	}
}
