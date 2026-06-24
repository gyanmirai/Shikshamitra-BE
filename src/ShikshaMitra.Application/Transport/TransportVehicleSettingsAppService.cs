using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize]
[RemoteService(false)]
public class TransportVehicleSettingsAppService(
	IRepository<TransportVehicleType, Guid> vehicleTypeRepository,
	IRepository<TransportRequiredVehicleDocument, Guid> requiredDocumentRepository,
	IRepository<TransportServiceType, Guid> serviceTypeRepository,
	IRepository<TransportFuelType, Guid> fuelTypeRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, ITransportVehicleSettingsAppService
{
	public async Task<List<TransportVehicleTypeDto>> GetVehicleTypesAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetVehicleTypesAsync));
		await EnsureDefaultVehicleTypesAsync(academicMediumId);
		var rows = await vehicleTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId);
		return rows.OrderBy(x => x.Name).Select(MapVehicleType).ToList();
	}

	public async Task<TransportVehicleTypeDto> CreateVehicleTypeAsync(Guid academicMediumId, CreateUpdateTransportVehicleTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateVehicleTypeAsync));
		ValidateName(input.Name, "Vehicle type name is required.");
		await EnsureVehicleTypeNameIsUniqueAsync(academicMediumId, input.Name);
		var row = new TransportVehicleType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, input.Name, input.ApproxSittingRange, input.Description, input.IsActive);
		await vehicleTypeRepository.InsertAsync(row, autoSave: true);
		return MapVehicleType(row);
	}

	public async Task<TransportVehicleTypeDto> UpdateVehicleTypeAsync(Guid vehicleTypeId, Guid academicMediumId, CreateUpdateTransportVehicleTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateVehicleTypeAsync));
		ValidateName(input.Name, "Vehicle type name is required.");
		var row = await GetVehicleTypeForMediumAsync(vehicleTypeId, academicMediumId);
		await EnsureVehicleTypeNameIsUniqueAsync(academicMediumId, input.Name, vehicleTypeId);
		row.UpdateDetails(input.Name, input.ApproxSittingRange, input.Description, input.IsActive);
		await vehicleTypeRepository.UpdateAsync(row, autoSave: true);
		return MapVehicleType(row);
	}

	public async Task DeleteVehicleTypeAsync(Guid vehicleTypeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteVehicleTypeAsync));
		await vehicleTypeRepository.DeleteAsync(await GetVehicleTypeForMediumAsync(vehicleTypeId, academicMediumId), autoSave: true);
	}

	public async Task<List<TransportRequiredVehicleDocumentDto>> GetRequiredVehicleDocumentsAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetRequiredVehicleDocumentsAsync));
		await EnsureDefaultRequiredDocumentsAsync(academicMediumId);
		var rows = await requiredDocumentRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId);
		return rows.OrderBy(x => x.Name).Select(MapRequiredDocument).ToList();
	}

	public async Task<TransportRequiredVehicleDocumentDto> CreateRequiredVehicleDocumentAsync(Guid academicMediumId, CreateUpdateTransportRequiredVehicleDocumentDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateRequiredVehicleDocumentAsync));
		ValidateName(input.Name, "Required document name is required.");
		ValidateDocumentInput(input);
		await EnsureRequiredDocumentNameIsUniqueAsync(academicMediumId, input.Name);
		var row = new TransportRequiredVehicleDocument(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, input.Name, input.MaxSizeMb, ToCsv(input.AcceptedFormats), input.IsRequired, input.IsActive);
		await requiredDocumentRepository.InsertAsync(row, autoSave: true);
		return MapRequiredDocument(row);
	}

	public async Task<TransportRequiredVehicleDocumentDto> UpdateRequiredVehicleDocumentAsync(Guid documentId, Guid academicMediumId, CreateUpdateTransportRequiredVehicleDocumentDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateRequiredVehicleDocumentAsync));
		ValidateName(input.Name, "Required document name is required.");
		ValidateDocumentInput(input);
		var row = await GetRequiredDocumentForMediumAsync(documentId, academicMediumId);
		await EnsureRequiredDocumentNameIsUniqueAsync(academicMediumId, input.Name, documentId);
		row.UpdateDetails(input.Name, input.MaxSizeMb, ToCsv(input.AcceptedFormats), input.IsRequired, input.IsActive);
		await requiredDocumentRepository.UpdateAsync(row, autoSave: true);
		return MapRequiredDocument(row);
	}

	public async Task DeleteRequiredVehicleDocumentAsync(Guid documentId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteRequiredVehicleDocumentAsync));
		await requiredDocumentRepository.DeleteAsync(await GetRequiredDocumentForMediumAsync(documentId, academicMediumId), autoSave: true);
	}

	public async Task<List<TransportServiceTypeDto>> GetServiceTypesAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetServiceTypesAsync));
		await EnsureDefaultServiceTypesAsync(academicMediumId);
		var rows = await serviceTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId);
		return rows.OrderBy(x => x.Name).Select(MapServiceType).ToList();
	}

	public async Task<TransportServiceTypeDto> CreateServiceTypeAsync(Guid academicMediumId, CreateUpdateTransportServiceTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateServiceTypeAsync));
		ValidateName(input.Name, "Service type name is required.");
		await EnsureServiceTypeNameIsUniqueAsync(academicMediumId, input.Name);
		var row = new TransportServiceType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, input.Name, input.Description, input.IsActive);
		await serviceTypeRepository.InsertAsync(row, autoSave: true);
		return MapServiceType(row);
	}

	public async Task<TransportServiceTypeDto> UpdateServiceTypeAsync(Guid serviceTypeId, Guid academicMediumId, CreateUpdateTransportServiceTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateServiceTypeAsync));
		ValidateName(input.Name, "Service type name is required.");
		var row = await GetServiceTypeForMediumAsync(serviceTypeId, academicMediumId);
		await EnsureServiceTypeNameIsUniqueAsync(academicMediumId, input.Name, serviceTypeId);
		row.UpdateDetails(input.Name, input.Description, input.IsActive);
		await serviceTypeRepository.UpdateAsync(row, autoSave: true);
		return MapServiceType(row);
	}

	public async Task DeleteServiceTypeAsync(Guid serviceTypeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteServiceTypeAsync));
		await serviceTypeRepository.DeleteAsync(await GetServiceTypeForMediumAsync(serviceTypeId, academicMediumId), autoSave: true);
	}

	public async Task<List<TransportFuelTypeDto>> GetFuelTypesAsync(Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetFuelTypesAsync));
		await EnsureDefaultFuelTypesAsync(academicMediumId);
		var rows = await fuelTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId);
		return rows.OrderBy(x => x.Name).Select(MapFuelType).ToList();
	}

	public async Task<TransportFuelTypeDto> CreateFuelTypeAsync(Guid academicMediumId, CreateUpdateTransportFuelTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateFuelTypeAsync));
		ValidateName(input.Name, "Fuel type name is required.");
		await EnsureFuelTypeNameIsUniqueAsync(academicMediumId, input.Name);
		var row = new TransportFuelType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, input.Name, input.Description, input.IsActive);
		await fuelTypeRepository.InsertAsync(row, autoSave: true);
		return MapFuelType(row);
	}

	public async Task<TransportFuelTypeDto> UpdateFuelTypeAsync(Guid fuelTypeId, Guid academicMediumId, CreateUpdateTransportFuelTypeDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateFuelTypeAsync));
		ValidateName(input.Name, "Fuel type name is required.");
		var row = await GetFuelTypeForMediumAsync(fuelTypeId, academicMediumId);
		await EnsureFuelTypeNameIsUniqueAsync(academicMediumId, input.Name, fuelTypeId);
		row.UpdateDetails(input.Name, input.Description, input.IsActive);
		await fuelTypeRepository.UpdateAsync(row, autoSave: true);
		return MapFuelType(row);
	}

	public async Task DeleteFuelTypeAsync(Guid fuelTypeId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteFuelTypeAsync));
		await fuelTypeRepository.DeleteAsync(await GetFuelTypeForMediumAsync(fuelTypeId, academicMediumId), autoSave: true);
	}

	public async Task<List<TransportOptionDto>> GetVehicleTypeOptionsAsync(Guid academicMediumId) =>
		(await GetVehicleTypesAsync(academicMediumId)).Where(x => x.IsActive).Select(x => ToOption(x.Id, x.Name)).ToList();

	public async Task<List<TransportOptionDto>> GetFuelTypeOptionsAsync(Guid academicMediumId) =>
		(await GetFuelTypesAsync(academicMediumId)).Where(x => x.IsActive).Select(x => ToOption(x.Id, x.Name)).ToList();

	public async Task<List<TransportOptionDto>> GetServiceTypeOptionsAsync(Guid academicMediumId) =>
		(await GetServiceTypesAsync(academicMediumId)).Where(x => x.IsActive).Select(x => ToOption(x.Id, x.Name)).ToList();

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private async Task EnsureDefaultVehicleTypesAsync(Guid academicMediumId)
	{
		if ((await vehicleTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId)).Any())
		{
			return;
		}

		foreach (var item in new[] { ("Bus", "20-60"), ("Van", "8-20"), ("Mini Bus", "12-30") })
		{
			await vehicleTypeRepository.InsertAsync(new TransportVehicleType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, item.Item1, item.Item2, null), autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task EnsureDefaultRequiredDocumentsAsync(Guid academicMediumId)
	{
		if ((await requiredDocumentRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId)).Any())
		{
			return;
		}

		foreach (var name in new[] { "RC Certificate", "Insurance Certificate", "Fitness Certificate", "PUC Certificate", "Road Tax Receipt" })
		{
			await requiredDocumentRepository.InsertAsync(new TransportRequiredVehicleDocument(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, name, 10, "0,1,2,4", true), autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task EnsureDefaultServiceTypesAsync(Guid academicMediumId)
	{
		if ((await serviceTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId)).Any())
		{
			return;
		}

		foreach (var name in new[] { "Regular", "Minor", "Major", "Emergency", "Body Work" })
		{
			await serviceTypeRepository.InsertAsync(new TransportServiceType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, name, null), autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task EnsureDefaultFuelTypesAsync(Guid academicMediumId)
	{
		if ((await fuelTypeRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId)).Any())
		{
			return;
		}

		foreach (var name in new[] { "Diesel", "Petrol", "CNG", "Electric" })
		{
			await fuelTypeRepository.InsertAsync(new TransportFuelType(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, name, null), autoSave: false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task<TransportVehicleType> GetVehicleTypeForMediumAsync(Guid id, Guid academicMediumId)
	{
		var row = await vehicleTypeRepository.FindAsync(id);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportVehicleType), id);
		}
		return row;
	}

	private async Task<TransportRequiredVehicleDocument> GetRequiredDocumentForMediumAsync(Guid id, Guid academicMediumId)
	{
		var row = await requiredDocumentRepository.FindAsync(id);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportRequiredVehicleDocument), id);
		}
		return row;
	}

	private async Task<TransportServiceType> GetServiceTypeForMediumAsync(Guid id, Guid academicMediumId)
	{
		var row = await serviceTypeRepository.FindAsync(id);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportServiceType), id);
		}
		return row;
	}

	private async Task<TransportFuelType> GetFuelTypeForMediumAsync(Guid id, Guid academicMediumId)
	{
		var row = await fuelTypeRepository.FindAsync(id);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportFuelType), id);
		}
		return row;
	}

	private async Task EnsureVehicleTypeNameIsUniqueAsync(Guid mediumId, string name, Guid? excludingId = null) =>
		await EnsureNameIsUniqueAsync(await vehicleTypeRepository.GetListAsync(x => x.AcademicMediumId == mediumId && (!excludingId.HasValue || x.Id != excludingId.Value)), name, TransportErrorCodes.VehicleTypeAlreadyExists);

	private async Task EnsureRequiredDocumentNameIsUniqueAsync(Guid mediumId, string name, Guid? excludingId = null) =>
		await EnsureNameIsUniqueAsync(await requiredDocumentRepository.GetListAsync(x => x.AcademicMediumId == mediumId && (!excludingId.HasValue || x.Id != excludingId.Value)), name, TransportErrorCodes.RequiredVehicleDocumentAlreadyExists);

	private async Task EnsureServiceTypeNameIsUniqueAsync(Guid mediumId, string name, Guid? excludingId = null) =>
		await EnsureNameIsUniqueAsync(await serviceTypeRepository.GetListAsync(x => x.AcademicMediumId == mediumId && (!excludingId.HasValue || x.Id != excludingId.Value)), name, TransportErrorCodes.ServiceTypeAlreadyExists);

	private async Task EnsureFuelTypeNameIsUniqueAsync(Guid mediumId, string name, Guid? excludingId = null) =>
		await EnsureNameIsUniqueAsync(await fuelTypeRepository.GetListAsync(x => x.AcademicMediumId == mediumId && (!excludingId.HasValue || x.Id != excludingId.Value)), name, TransportErrorCodes.FuelTypeAlreadyExists);

	private static Task EnsureNameIsUniqueAsync<T>(IEnumerable<T> rows, string name, string errorCode)
	{
		if (rows.Any(row => NormalizeName((string)row!.GetType().GetProperty("Name")!.GetValue(row)!) == NormalizeName(name)))
		{
			throw new BusinessException(errorCode).WithData("Name", name);
		}

		return Task.CompletedTask;
	}

	private static void ValidateName(string name, string message)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new UserFriendlyException(message);
		}
	}

	private static void ValidateDocumentInput(CreateUpdateTransportRequiredVehicleDocumentDto input)
	{
		if (input.MaxSizeMb <= 0 || input.MaxSizeMb > 10)
		{
			throw new UserFriendlyException("Document size must be between 1 and 10 MB.");
		}

		if (input.AcceptedFormats.Count == 0)
		{
			throw new UserFriendlyException("At least one accepted format is required.");
		}
	}

	private static TransportVehicleTypeDto MapVehicleType(TransportVehicleType row) => new()
	{
		Id = row.Id,
		AcademicMediumId = row.AcademicMediumId,
		Name = row.Name,
		TypeName = row.Name,
		ApproxSittingRange = row.ApproxSittingRange,
		Description = row.Description,
		IsActive = row.IsActive,
		CreationTime = row.CreationTime,
		CreatorId = row.CreatorId,
		LastModificationTime = row.LastModificationTime,
		LastModifierId = row.LastModifierId
	};

	private static TransportRequiredVehicleDocumentDto MapRequiredDocument(TransportRequiredVehicleDocument row)
	{
		var acceptedFormats = FromCsv(row.AcceptedFormatsCsv);
		return new TransportRequiredVehicleDocumentDto
		{
			Id = row.Id,
			AcademicMediumId = row.AcademicMediumId,
			Name = row.Name,
			DocumentName = row.Name,
			MaxSizeMb = row.MaxSizeMb,
			DocumentSize = row.MaxSizeMb,
			AcceptedFormats = acceptedFormats,
			AcceptedFormat = acceptedFormats,
			IsRequired = row.IsRequired,
			Required = row.IsRequired,
			IsActive = row.IsActive,
			CreationTime = row.CreationTime,
			CreatorId = row.CreatorId,
			LastModificationTime = row.LastModificationTime,
			LastModifierId = row.LastModifierId
		};
	}

	private static TransportServiceTypeDto MapServiceType(TransportServiceType row) => new()
	{
		Id = row.Id,
		AcademicMediumId = row.AcademicMediumId,
		Name = row.Name,
		TypeName = row.Name,
		Description = row.Description,
		IsActive = row.IsActive,
		CreationTime = row.CreationTime,
		CreatorId = row.CreatorId,
		LastModificationTime = row.LastModificationTime,
		LastModifierId = row.LastModifierId
	};

	private static TransportFuelTypeDto MapFuelType(TransportFuelType row) => new()
	{
		Id = row.Id,
		AcademicMediumId = row.AcademicMediumId,
		Name = row.Name,
		TypeName = row.Name,
		Description = row.Description,
		IsActive = row.IsActive,
		CreationTime = row.CreationTime,
		CreatorId = row.CreatorId,
		LastModificationTime = row.LastModificationTime,
		LastModifierId = row.LastModifierId
	};

	private static TransportOptionDto ToOption(Guid id, string name) => new()
	{
		Id = id,
		Label = name,
		Value = id.ToString(),
		Name = name
	};

	private static string ToCsv(IEnumerable<int> values) =>
		string.Join(",", values.Distinct().OrderBy(x => x));

	private static List<int> FromCsv(string value) =>
		value.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(part => int.TryParse(part.Trim(), out var parsed) ? parsed : (int?)null)
			.Where(parsed => parsed.HasValue)
			.Select(parsed => parsed!.Value)
			.ToList();

	private static string NormalizeName(string value) =>
		string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
