using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.TenantSettings.DocumentTypes;

/// <summary>
/// Domain service for DocumentType aggregate
/// Handles business rules and validations
/// </summary>
public class DocumentTypeManager : DomainService
{
	private readonly IRepository<DocumentType, Guid> _documentTypeRepository;

	public DocumentTypeManager(IRepository<DocumentType, Guid> documentTypeRepository)
	{
		_documentTypeRepository = documentTypeRepository;
	}

	/// <summary>
	/// Creates a new document type with uniqueness validation
	/// </summary>
	public async Task<DocumentType> CreateAsync(
		Guid? tenantId,
		string name,
		double maxSizeMB,
		List<AcceptedFileFormat> acceptedFormats,
		bool isRequired = false,
		int displayOrder = 0)
	{
		// Check if document type with same name already exists for this tenant
		await CheckNameUniquenessAsync(tenantId, name);

		var documentType = new DocumentType(
			GuidGenerator.Create(),
			tenantId,
			name,
			maxSizeMB,
			acceptedFormats,
			isRequired,
			displayOrder
		);

		return documentType;
	}

	/// <summary>
	/// Updates an existing document type with uniqueness validation
	/// </summary>
	public async Task<DocumentType> UpdateAsync(
		DocumentType documentType,
		string name,
		double maxSizeMB,
		List<AcceptedFileFormat> acceptedFormats,
		bool isRequired,
		int displayOrder)
	{
		// Check uniqueness only if name is changing
		if (documentType.Name != name)
		{
			await CheckNameUniquenessAsync(documentType.TenantId, name, documentType.Id);
		}

		documentType.Update(name, maxSizeMB, acceptedFormats, isRequired, displayOrder);

		return documentType;
	}

	/// <summary>
	/// Checks if a document type name is unique within the tenant
	/// </summary>
	private async Task CheckNameUniquenessAsync(
		Guid? tenantId,
		string name,
		Guid? excludeId = null)
	{
		var query = await _documentTypeRepository.GetQueryableAsync();

		if (await AsyncExecuter.AnyAsync(query.Where(x =>
			x.TenantId == tenantId &&
			x.Name == name &&
			(excludeId == null || x.Id != excludeId))))
		{
			throw new BusinessException("DocumentType:DuplicateName")
				.WithData("Name", name);
		}
	}
}
