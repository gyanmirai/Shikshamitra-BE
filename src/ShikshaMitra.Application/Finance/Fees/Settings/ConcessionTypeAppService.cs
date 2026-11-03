using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Cache;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

[Authorize]
[RemoteService(false)]
public partial class ConcessionTypeAppService(
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<ConcessionRequest, Guid> concessionRequestRepository,
	FinanceReferenceCacheService referenceCache,
	IGuidGenerator guidGenerator,
	IDataFilter<ISoftDelete> softDeleteFilter,
	IAuditLogRepository auditLogRepository,
	IAuditingManager auditingManager,
	IRepository<IdentityUser, Guid> userRepository
) : ApplicationService, IConcessionTypeAppService
{
	private const int MaxPageSize = 1000;

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Add)]
	public async Task CreateAsync(Guid academicYearId, ConcessionTypeRequest input)
	{
		_ = await academicYearRepository.GetAsync(academicYearId);
		await EnsureNameIsUniqueAsync(academicYearId, input.Name);

		var concessionType = new ConcessionType(
			guidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.Name,
			input.Description);

		AddDocuments(concessionType, input.Documents);
		await concessionTypeRepository.InsertAsync(concessionType, autoSave: true);
		RecordDocumentChanges(concessionType, input.Documents
			.OrderBy(document => ConcessionTypeConsts.NormalizeNameKey(document.DocumentName), StringComparer.Ordinal)
			.Select((document, index) => new EntityPropertyChangeInfo
			{
				PropertyName = $"Document {index + 1}",
				PropertyTypeFullName = typeof(string).FullName!,
				NewValue = FormatDocumentDefinition(document.DocumentName, document.MaxFileSizeBytes,
					document.IsRequired, document.AcceptedFormats)
			}).ToList(), EntityChangeType.Created);
		await referenceCache.InvalidateConcessionTypesAsync();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<PagedResultDto<ConcessionTypeResponse>> GetListAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null,
		bool includeInactive = false,
		bool? isActive = null, string? filters = null)
	{
		var tableQuery = ListFilter.Parse(filters);
		if (academicYearId == Guid.Empty || skipCount < 0 || maxResultCount is < 1 or > MaxPageSize)
			throw new BusinessException("Finance:ConcessionType:InvalidPaging");
		var query = await concessionTypeRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (isActive.HasValue)
		{
			query = query.Where(x => x.IsActive == isActive.Value);
		}
		else if (!includeInactive)
		{
			query = query.Where(x => x.IsActive);
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearch = ConcessionTypeConsts.NormalizeNameKey(searchTerm);
			query = query.Where(x => x.NormalizedName.Contains(normalizedSearch));
		}

		query = await ApplyListFiltersAsync(query, tableQuery);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var result = await AsyncExecuter.ToListAsync(
			query.OrderBy(x => x.Name)
				.Skip(Math.Max(0, skipCount))
				.Take(Math.Clamp(maxResultCount, 1, MaxPageSize)));
		var usedIds = await GetUsedIdsAsync(result.Select(x => x.Id));

		return new PagedResultDto<ConcessionTypeResponse>(
			totalCount,
			result.Select(x => MapResponse(x, usedIds.Contains(x.Id))).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<ConcessionTypeResponse> GetAsync(Guid id)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		var isUsed = await IsUsedAsync(id);
		return MapResponse(concessionType, isUsed);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public async Task<PagedResultDto<ConcessionTypeVersionHistoryItemDto>> GetVersionHistoryAsync(
		Guid id,
		int skipCount = 0,
		int maxResultCount = 50)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		var entityTypeName = typeof(ConcessionType).FullName!;
		var entityId = id.ToString();
		var changes = await auditLogRepository.GetEntityChangeListAsync(
			sorting: "ChangeTime asc",
			maxResultCount: 1000,
			skipCount: 0,
			entityId: entityId,
			entityTypeFullName: entityTypeName,
			includeDetails: true);

		var auditLogIds = changes.Select(change => change.AuditLogId).Distinct().ToList();
		var auditLogs = auditLogIds.Count == 0
			? []
			: await auditLogRepository.GetListAsync(log => auditLogIds.Contains(log.Id));
		var auditLogsById = auditLogs.ToDictionary(log => log.Id);
		var actorIds = auditLogs
			.Where(log => log.UserId.HasValue)
			.Select(log => log.UserId!.Value)
			.Append(concessionType.CreatorId ?? Guid.Empty)
			.Where(actorId => actorId != Guid.Empty)
			.Distinct()
			.ToList();
		var users = actorIds.Count == 0
			? []
			: await userRepository.GetListAsync(user => actorIds.Contains(user.Id));
		var actorNames = users.ToDictionary(
			user => user.Id,
			user => string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name);

		var history = changes.Select((change, index) =>
		{
			auditLogsById.TryGetValue(change.AuditLogId, out var auditLog);
			var actorId = auditLog?.UserId;
			return new ConcessionTypeVersionHistoryItemDto
			{
				Id = change.Id,
				VersionNumber = index + 1,
				ChangeAction = change.ChangeType.ToString().ToLowerInvariant(),
				ChangeTime = change.ChangeTime,
				ActorId = actorId,
				ActorName = actorId.HasValue ? actorNames.GetValueOrDefault(actorId.Value) : null,
				ChangedFields = change.PropertyChanges
					.Where(propertyChange => IsVisibleHistoryField(propertyChange.PropertyName))
					.Select(propertyChange => new ConcessionTypeVersionFieldChangeDto
					{
						FieldName = propertyChange.PropertyName,
						OriginalValue = FormatHistoryValue(propertyChange.PropertyName, propertyChange.OriginalValue),
						NewValue = FormatHistoryValue(propertyChange.PropertyName, propertyChange.NewValue)
					})
					.ToList()
			};
		}).ToList();

		if (!history.Any(item => item.ChangeAction == "created"))
		{
			history.Insert(0, new ConcessionTypeVersionHistoryItemDto
			{
				Id = concessionType.Id,
				VersionNumber = 1,
				ChangeAction = "created",
				ChangeTime = concessionType.CreationTime,
				ActorId = concessionType.CreatorId,
				ActorName = concessionType.CreatorId.HasValue
					? actorNames.GetValueOrDefault(concessionType.CreatorId.Value)
					: null
			});
			for (var index = 1; index < history.Count; index++)
			{
				history[index].VersionNumber = index + 1;
			}
		}

		var ordered = history.OrderByDescending(item => item.VersionNumber).ToList();
		return new PagedResultDto<ConcessionTypeVersionHistoryItemDto>(
			ordered.Count,
			ordered.Skip(Math.Max(0, skipCount)).Take(Math.Clamp(maxResultCount, 1, 100)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.View)]
	public Task<ConcessionTypeDocumentDefaultsResponse> GetDocumentDefaultsAsync() =>
		Task.FromResult(new ConcessionTypeDocumentDefaultsResponse
		{
			DefaultMaxFileSizeBytes = ConcessionTypeConsts.DefaultMaxFileSizeBytes,
			AbsoluteMaxFileSizeBytes = ConcessionTypeConsts.AbsoluteMaxFileSizeBytes,
			DefaultAcceptedFormats = ConcessionTypeConsts.DefaultAcceptedFormats.ToList(),
			AllowedFormats = Enum.GetValues<ShikshaMitra.Common.DocumentType>().ToList()
		});

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Edit)]
	public async Task UpdateAsync(Guid id, ConcessionTypeRequest input)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		EnsureConcurrency(concessionType, input.ConcurrencyStamp);
		await EnsureNameIsUniqueAsync(concessionType.AcademicYearId, input.Name, id);

		var isUsed = await IsUsedAsync(id);
		var documentsChanged = DocumentDefinitionsChanged(concessionType, input.Documents);
		if (isUsed && documentsChanged)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.DocumentDefinitionsLocked);
		}

		var previousName = concessionType.Name;
		var previousDescription = concessionType.Description;
		concessionType.Update(input.Name, input.Description);
		if (!documentsChanged && previousName == concessionType.Name &&
			previousDescription == concessionType.Description)
			return;
		var documentChanges = documentsChanged
			? GetDocumentChanges(concessionType, input.Documents)
			: [];
		if (documentsChanged)
		{
			concessionType.ReplaceDocuments(input.Documents.Select(ToDocumentTuple).ToList());
		}

		await concessionTypeRepository.UpdateAsync(concessionType, autoSave: true);
		RecordDocumentChanges(concessionType, documentChanges);
		await referenceCache.InvalidateConcessionTypesAsync();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.ChangeStatus)]
	public async Task<ConcessionTypeResponse> UpdateStatusAsync(
		Guid id,
		ConcessionTypeStatusUpdateRequest input)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		EnsureConcurrency(concessionType, input.ConcurrencyStamp);

		if (input.IsActive)
		{
			concessionType.Activate();
		}
		else
		{
			concessionType.Deactivate(input.DeactivationReason ?? string.Empty);
		}

		await concessionTypeRepository.UpdateAsync(concessionType, autoSave: true);
		await referenceCache.InvalidateConcessionTypesAsync();
		var isUsed = await IsUsedAsync(id);
		return MapResponse(concessionType, isUsed);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.Delete)]
	[UnitOfWork]
	public async Task DeleteAsync(Guid id)
	{
		var concessionType = await concessionTypeRepository.GetAsync(id);
		if (concessionType.IsActive)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.ActiveCannotBeDeleted)
				.WithData("Name", concessionType.Name);
		}
		if (await IsUsedAsync(id))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.CannotDeleteInUse);
		}

		await concessionTypeRepository.DeleteAsync(concessionType, autoSave: true);
		await referenceCache.InvalidateConcessionTypesAsync();
	}

	private async Task EnsureNameIsUniqueAsync(Guid academicYearId, string name, Guid? excludingId = null)
	{
		var normalizedName = ConcessionTypeConsts.NormalizeNameKey(name);
		var exists = await concessionTypeRepository.AnyAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.NormalizedName == normalizedName &&
			(!excludingId.HasValue || x.Id != excludingId.Value));
		if (exists)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.DuplicateName)
				.WithData("Name", ConcessionTypeConsts.NormalizeDisplayName(name));
		}
	}

	private async Task<HashSet<Guid>> GetUsedIdsAsync(IEnumerable<Guid> ids)
	{
		var idList = ids.Distinct().ToList();
		if (idList.Count == 0)
		{
			return [];
		}

		using var _ = softDeleteFilter.Disable();
		var requests = await concessionRequestRepository.GetListAsync(x => idList.Contains(x.ConcessionTypeId));
		return requests.Select(x => x.ConcessionTypeId).ToHashSet();
	}

	private async Task<bool> IsUsedAsync(Guid concessionTypeId)
	{
		using var _ = softDeleteFilter.Disable();
		return await concessionRequestRepository.AnyAsync(x => x.ConcessionTypeId == concessionTypeId);
	}

	private static void AddDocuments(ConcessionType concessionType, IEnumerable<ConcessionTypeDocumentDto> documents)
	{
		foreach (var document in documents)
		{
			concessionType.AddDocument(
				document.DocumentName,
				document.MaxFileSizeBytes,
				document.AcceptedFormats,
				document.IsRequired);
		}
	}

	private static (
		string DocumentName,
		long DocumentSize,
		List<ShikshaMitra.Common.DocumentType> AcceptedFormats,
		bool IsRequired) ToDocumentTuple(ConcessionTypeDocumentDto document) =>
		(document.DocumentName, document.MaxFileSizeBytes, document.AcceptedFormats, document.IsRequired);

	private static bool DocumentDefinitionsChanged(
		ConcessionType concessionType,
		IEnumerable<ConcessionTypeDocumentDto> incomingDocuments)
	{
		var current = concessionType.ConcessionTypeDocuments
			.Select(document => DocumentDefinitionKey(
				document.DocumentName,
				document.MaxFileSizeBytes,
				document.IsRequired,
				document.AcceptedFormats.Select(format => format.DocumentType)))
			.OrderBy(value => value, StringComparer.Ordinal)
			.ToList();
		var incoming = incomingDocuments
			.Select(document => DocumentDefinitionKey(
				document.DocumentName,
				document.MaxFileSizeBytes,
				document.IsRequired,
				document.AcceptedFormats))
			.OrderBy(value => value, StringComparer.Ordinal)
			.ToList();
		return !current.SequenceEqual(incoming, StringComparer.Ordinal);
	}

	private static string DocumentDefinitionKey(
		string name,
		long size,
		bool isRequired,
		IEnumerable<ShikshaMitra.Common.DocumentType> formats) =>
		$"{ConcessionTypeConsts.NormalizeNameKey(name)}|{size}|{isRequired}|{string.Join(',', formats.Order())}";

	private static List<EntityPropertyChangeInfo> GetDocumentChanges(
		ConcessionType concessionType,
		IEnumerable<ConcessionTypeDocumentDto> incomingDocuments)
	{
		var before = concessionType.ConcessionTypeDocuments.ToDictionary(
			document => ConcessionTypeConsts.NormalizeNameKey(document.DocumentName),
			document => FormatDocumentDefinition(document.DocumentName, document.MaxFileSizeBytes,
				document.IsRequired, document.AcceptedFormats.Select(format => format.DocumentType)));
		var after = incomingDocuments.ToDictionary(
			document => ConcessionTypeConsts.NormalizeNameKey(document.DocumentName),
			document => FormatDocumentDefinition(document.DocumentName, document.MaxFileSizeBytes,
				document.IsRequired, document.AcceptedFormats));

		return before.Keys.Union(after.Keys).Order(StringComparer.Ordinal)
			.Select((key, index) => new { Key = key, Index = index })
			.Where(item => before.GetValueOrDefault(item.Key) != after.GetValueOrDefault(item.Key))
			.Select(item => new EntityPropertyChangeInfo
			{
				PropertyName = $"Document {item.Index + 1}",
				PropertyTypeFullName = typeof(string).FullName!,
				OriginalValue = before.GetValueOrDefault(item.Key),
				NewValue = after.GetValueOrDefault(item.Key)
			}).ToList();
	}

	private static string FormatDocumentDefinition(
		string name,
		long maxFileSizeBytes,
		bool isRequired,
		IEnumerable<ShikshaMitra.Common.DocumentType> acceptedFormats) =>
		$"{name.Trim()} · Max file size: {maxFileSizeBytes} bytes · " +
		$"Accepted formats: {string.Join(", ", acceptedFormats.Order())} · " +
		$"{(isRequired ? "Mandatory" : "Optional")}";

	private void RecordDocumentChanges(
		ConcessionType concessionType,
		List<EntityPropertyChangeInfo> documentChanges,
		EntityChangeType changeType = EntityChangeType.Updated)
	{
		if (documentChanges.Count == 0 || auditingManager.Current?.Log is not { } auditLog)
			return;

		// ABP audits document children separately; attach their readable diff to the
		// parent event consumed by the Concession Type version-history endpoint.
		var entityChange = auditLog.EntityChanges.LastOrDefault(change =>
			change.EntityTypeFullName == typeof(ConcessionType).FullName &&
			change.EntityId == concessionType.Id.ToString() &&
			change.ChangeType == changeType);
		if (entityChange is null)
		{
			entityChange = new EntityChangeInfo
			{
				EntityTypeFullName = typeof(ConcessionType).FullName,
				EntityId = concessionType.Id.ToString(),
				EntityTenantId = concessionType.TenantId,
				ChangeTime = DateTime.UtcNow,
				ChangeType = changeType,
				PropertyChanges = []
			};
			auditLog.EntityChanges.Add(entityChange);
		}

		entityChange.PropertyChanges.AddRange(documentChanges);
	}

	private static ConcessionTypeResponse MapResponse(ConcessionType concessionType, bool isUsed) => new()
	{
		Id = concessionType.Id,
		Name = concessionType.Name,
		Description = concessionType.Description,
		IsActive = concessionType.IsActive,
		DeactivationReason = concessionType.DeactivationReason,
		IsUsedInDownstream = isUsed,
		IsDocumentRequired = concessionType.ConcessionTypeDocuments.Any(document => document.IsRequired),
		ConcurrencyStamp = concessionType.ConcurrencyStamp,
		Documents = concessionType.ConcessionTypeDocuments.Select(document => new ConcessionTypeDocumentDto
		{
			Id = document.Id,
			DocumentName = document.DocumentName,
			MaxFileSizeBytes = document.MaxFileSizeBytes,
			IsRequired = document.IsRequired,
			AcceptedFormats = document.AcceptedFormats.Select(format => format.DocumentType).ToList()
		}).ToList()
	};

	private static void EnsureConcurrency(ConcessionType concessionType, string? concurrencyStamp)
	{
		if (!string.IsNullOrWhiteSpace(concurrencyStamp) &&
			!string.Equals(concessionType.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
		{
			throw new AbpDbConcurrencyException(
				$"Concession Type {concessionType.Id} was modified by another user.");
		}
	}

	private static bool IsVisibleHistoryField(string propertyName) => propertyName is
		nameof(ConcessionType.Name) or
		nameof(ConcessionType.Description) or
		nameof(ConcessionType.IsActive) or
		nameof(ConcessionType.DeactivationReason) or
		nameof(ConcessionType.DiscountType) ||
		propertyName.StartsWith("Document ", StringComparison.Ordinal);

	private static string? FormatHistoryValue(string propertyName, string? value)
	{
		if (propertyName != nameof(ConcessionType.DiscountType) || string.IsNullOrWhiteSpace(value))
			return value;

		var normalized = value.Trim().Trim('"');
		if (string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase))
			return null;
		if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
			Enum.IsDefined(typeof(AmountType), number))
			return ((AmountType)number).ToString();

		return Enum.TryParse<AmountType>(normalized, true, out var amountType) &&
			Enum.IsDefined(amountType)
			? amountType.ToString()
			: value;
	}
}
