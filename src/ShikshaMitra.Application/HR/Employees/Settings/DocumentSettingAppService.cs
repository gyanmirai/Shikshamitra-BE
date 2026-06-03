using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
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
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class DocumentSettingAppService
	: CrudAppService<DocumentSetting, DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
			CreateUpdateDocumentSettingDto>,
		IDocumentSettingAppService
{
	private readonly IDocumentSettingRepository _repository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ILogger<DocumentSettingAppService> _logger;

	public DocumentSettingAppService(
		IDocumentSettingRepository repository,
		IGuidGenerator guidGenerator,
		ILogger<DocumentSettingAppService> logger)
		: base(repository)
	{
		_repository = repository;
		_guidGenerator = guidGenerator;
		_logger = logger;

		GetPolicyName = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View;
		GetListPolicyName = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View;
		CreatePolicyName = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add;
		UpdatePolicyName = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add;
		DeletePolicyName = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Delete;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public override async Task<DocumentSettingDto> CreateAsync(CreateUpdateDocumentSettingDto input)
	{
		try
		{
			var exists = await _repository.FindAsync(x => x.DocumentName == input.DocumentName);
			if (exists != null)
			{
				throw new BusinessException(HRErrorCodes.EmployeeDocumentSettingAlreadyExists)
					.WithData("documentName", input.DocumentName);
			}
			return await base.CreateAsync(input);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating document setting");
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

		var totalCount = await AsyncExecuter.CountAsync(queryable);

		var items = await AsyncExecuter.ToListAsync(
			queryable.OrderBy(x => x.DocumentName)
				.Skip(skipCount)
				.Take(pageSize)
		);

		return DocumentSettingProjection.ToListResponse(items, totalCount, page, pageSize);
	}

	/// <summary>
	/// Get document configuration by ID (matches OpenAPI contract)
	/// </summary>
	public async Task<DocumentItem> GetDocumentByIdAsync(Guid documentId)
	{
		var doc = await Repository.GetAsync(documentId);

		return DocumentSettingProjection.ToItem(doc);
	}

	/// <summary>
	/// Create document configuration (matches OpenAPI contract)
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<DocumentCreateResponse> CreateDocumentAsync(DocumentRequest input)
	{
		var exists = await _repository.FindAsync(x => x.DocumentName == input.Title);
		if (exists != null)
		{
			throw new BusinessException(HRErrorCodes.EmployeeDocumentSettingAlreadyExists)
				.WithData("documentName", input.Title);
		}

		var doc = new DocumentSetting(
			_guidGenerator.Create(),
			CurrentTenant.Id,
			input.Title,
			input.DocumentSize,
			isRequired: false,
			DocumentSettingProjection.ToDocumentTypes(input.DocumentTypes)
		);

		await Repository.InsertAsync(doc, autoSave: true);

		return DocumentSettingProjection.ToCreateResponse(doc);
	}

	/// <summary>
	/// Update document configuration (matches OpenAPI contract)
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UpdateDocumentAsync(Guid documentId, DocumentRequest input)
	{
		var doc = await Repository.GetAsync(documentId);

		var duplicate = await _repository.FindAsync(x => x.DocumentName == input.Title && x.Id != documentId);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.EmployeeDocumentSettingAlreadyExists)
				.WithData("documentName", input.Title);
		}

		doc.Update(
			input.Title,
			input.DocumentSize,
			doc.IsRequired,
			DocumentSettingProjection.ToDocumentTypes(input.DocumentTypes));

		await Repository.UpdateAsync(doc, autoSave: true);
	}

	/// <summary>
	/// Delete document configuration (matches OpenAPI contract)
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Delete)]
	public async Task DeleteDocumentAsync(Guid documentId)
	{
		var doc = await Repository.GetAsync(documentId);
		await Repository.DeleteAsync(doc, autoSave: true);
	}
}
