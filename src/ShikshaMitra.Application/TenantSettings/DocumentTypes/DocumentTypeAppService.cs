using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.DocumentTypes;

/// <summary>
/// Application service for managing document types
/// </summary>
[RemoteService(false)]
public class DocumentTypeAppService : ApplicationService, IDocumentTypeAppService
{
	private readonly IRepository<DocumentType, Guid> _documentTypeRepository;
	private readonly DocumentTypeManager _documentTypeManager;

	public DocumentTypeAppService(
		IRepository<DocumentType, Guid> documentTypeRepository,
		DocumentTypeManager documentTypeManager)
	{
		_documentTypeRepository = documentTypeRepository;
		_documentTypeManager = documentTypeManager;
	}

	/// <summary>
	/// Gets a paginated list of document types for the current tenant
	/// </summary>
	public async Task<PagedResultDto<DocumentTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await _documentTypeRepository.GetQueryableAsync();

		// Filter by current tenant
		var query = queryable
			.Where(x => x.TenantId == CurrentTenant.Id)
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Name);

		var totalCount = await AsyncExecuter.CountAsync(query);

		if (totalCount == 0)
		{
			return new PagedResultDto<DocumentTypeDto>(0, new List<DocumentTypeDto>());
		}

		var pagedQuery = query
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		var documentTypes = await AsyncExecuter.ToListAsync(pagedQuery);
		
		// Map manually to handle enum conversion
		var dtos = documentTypes.Select(dt => new DocumentTypeDto
		{
			Id = dt.Id,
			Name = dt.Name,
			MaxSizeMB = dt.MaxSizeMB,
			AcceptedFormats = dt.GetAcceptedFormats(),
			IsRequired = dt.IsRequired,
			DisplayOrder = dt.DisplayOrder,
			CreationTime = dt.CreationTime,
			CreatorId = dt.CreatorId,
			LastModificationTime = dt.LastModificationTime,
			LastModifierId = dt.LastModifierId
		}).ToList();

		return new PagedResultDto<DocumentTypeDto>(totalCount, dtos);
	}

	/// <summary>
	/// Gets a document type by ID
	/// </summary>
	public async Task<DocumentTypeDto> GetAsync(Guid id)
	{
		var documentType = await _documentTypeRepository.GetAsync(id);

		// Verify tenant isolation
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFound")
				.WithData("Id", id);
		}

		return new DocumentTypeDto
		{
			Id = documentType.Id,
			Name = documentType.Name,
			MaxSizeMB = documentType.MaxSizeMB,
			AcceptedFormats = documentType.GetAcceptedFormats(),
			IsRequired = documentType.IsRequired,
			DisplayOrder = documentType.DisplayOrder,
			CreationTime = documentType.CreationTime,
			CreatorId = documentType.CreatorId,
			LastModificationTime = documentType.LastModificationTime,
			LastModifierId = documentType.LastModifierId
		};
	}

	/// <summary>
	/// Creates a new document type
	/// </summary>
	public async Task<DocumentTypeDto> CreateAsync(CreateUpdateDocumentTypeDto input)
	{
		var documentType = await _documentTypeManager.CreateAsync(
			CurrentTenant.Id,
			input.Name,
			input.MaxSizeMB,
			input.AcceptedFormats,
			input.IsRequired,
			input.DisplayOrder
		);

		await _documentTypeRepository.InsertAsync(documentType, autoSave: true);

		return new DocumentTypeDto
		{
			Id = documentType.Id,
			Name = documentType.Name,
			MaxSizeMB = documentType.MaxSizeMB,
			AcceptedFormats = documentType.GetAcceptedFormats(),
			IsRequired = documentType.IsRequired,
			DisplayOrder = documentType.DisplayOrder,
			CreationTime = documentType.CreationTime,
			CreatorId = documentType.CreatorId
		};
	}

	/// <summary>
	/// Updates an existing document type
	/// </summary>
	public async Task<DocumentTypeDto> UpdateAsync(Guid id, CreateUpdateDocumentTypeDto input)
	{
		var documentType = await _documentTypeRepository.GetAsync(id);

		// Verify tenant isolation
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFound")
				.WithData("Id", id);
		}

		await _documentTypeManager.UpdateAsync(
			documentType,
			input.Name,
			input.MaxSizeMB,
			input.AcceptedFormats,
			input.IsRequired,
			input.DisplayOrder
		);

		await _documentTypeRepository.UpdateAsync(documentType, autoSave: true);

		return new DocumentTypeDto
		{
			Id = documentType.Id,
			Name = documentType.Name,
			MaxSizeMB = documentType.MaxSizeMB,
			AcceptedFormats = documentType.GetAcceptedFormats(),
			IsRequired = documentType.IsRequired,
			DisplayOrder = documentType.DisplayOrder,
			CreationTime = documentType.CreationTime,
			CreatorId = documentType.CreatorId,
			LastModificationTime = documentType.LastModificationTime,
			LastModifierId = documentType.LastModifierId
		};
	}

	/// <summary>
	/// Deletes a document type
	/// </summary>
	public async Task DeleteAsync(Guid id)
	{
		var documentType = await _documentTypeRepository.GetAsync(id);

		// Verify tenant isolation
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFound")
				.WithData("Id", id);
		}

		await _documentTypeRepository.DeleteAsync(documentType, autoSave: true);
	}
}
