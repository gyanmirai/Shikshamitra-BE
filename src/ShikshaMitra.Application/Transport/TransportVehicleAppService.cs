using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Transport;

[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
[RemoteService(false)]
public class TransportVehicleAppService(
	IRepository<TransportVehicle, Guid> vehicleRepository,
	IRepository<TransportVehicleType, Guid> vehicleTypeRepository,
	IRepository<TransportFuelType, Guid> fuelTypeRepository,
	IRepository<TransportRequiredVehicleDocument, Guid> requiredDocumentRepository,
	IRepository<TransportVehicleDocument, Guid> vehicleDocumentRepository,
	IRepository<TransportVehicleCompliance, Guid> complianceRepository,
	IRepository<TransportVehicleMaintenanceRecord, Guid> maintenanceRepository,
	IRepository<TransportRouteAssignment, Guid> assignmentRepository,
	IRepository<TransportRoute, Guid> routeRepository,
	IRepository<TransportStaff, Guid> staffRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	StudentScopeAccessService studentScopeAccessService,
	IBlobContainer blobContainer
) : ApplicationService, ITransportVehicleAppService
{
	public async Task<PagedResultDto<TransportVehicleDto>> GetListAsync(TransportVehicleListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetListAsync));

		var vehicleQuery = await vehicleRepository.GetQueryableAsync();
		vehicleQuery = vehicleQuery.Where(x => x.AcademicMediumId == input.AcademicMediumId);

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			vehicleQuery = vehicleQuery.Where(x =>
				x.RegistrationNo.ToLower().Contains(search) ||
				x.VehicleNo.ToLower().Contains(search) ||
				(x.VehicleName != null && x.VehicleName.ToLower().Contains(search)) ||
				x.VehicleCompany.ToLower().Contains(search) ||
				x.Model.ToLower().Contains(search));
		}

		if (input.Status.HasValue)
		{
			vehicleQuery = vehicleQuery.Where(x => x.Status == input.Status.Value);
		}

		if (input.VehicleTypeId.HasValue)
		{
			vehicleQuery = vehicleQuery.Where(x => x.VehicleTypeId == input.VehicleTypeId.Value);
		}

		if (input.FuelTypeId.HasValue)
		{
			vehicleQuery = vehicleQuery.Where(x => x.FuelTypeId == input.FuelTypeId.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(vehicleQuery);
		var vehicles = await AsyncExecuter.ToListAsync(
			vehicleQuery
				.OrderBy(x => x.RegistrationNo)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportVehicleDto>(
			totalCount,
			await MapVehiclesAsync(vehicles));
	}

	public async Task<TransportVehicleDto> GetAsync(Guid vehicleId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetAsync));
		var vehicle = await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		return (await MapVehiclesAsync([vehicle])).Single();
	}

	public async Task<TransportVehicleDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportVehicleDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(CreateAsync));
		ValidateVehicleInput(input);
		await EnsureVehicleTypeExistsAsync(academicMediumId, input.VehicleTypeId);
		await EnsureFuelTypeExistsAsync(academicMediumId, input.FuelTypeId);
		await EnsureRegistrationIsUniqueAsync(academicMediumId, input.RegistrationNo);

		var vehicle = new TransportVehicle(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			input.RegistrationNo,
			input.VehicleNo,
			input.VehicleName,
			input.VehicleTypeId,
			input.VehicleCompany,
			input.Model,
			input.YearOfManufacture,
			input.SeatingCapacity,
			input.VehicleColor,
			input.SchoolAssignedNo,
			input.Owner,
			input.FuelTypeId,
			input.IsAc,
			input.Description);

		await vehicleRepository.InsertAsync(vehicle, autoSave: true);
		return await GetAsync(vehicle.Id, academicMediumId);
	}

	public async Task<TransportVehicleDto> UpdateAsync(Guid vehicleId, Guid academicMediumId, CreateUpdateTransportVehicleDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateAsync));
		ValidateVehicleInput(input);
		await EnsureVehicleTypeExistsAsync(academicMediumId, input.VehicleTypeId);
		await EnsureFuelTypeExistsAsync(academicMediumId, input.FuelTypeId);
		var vehicle = await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		await EnsureRegistrationIsUniqueAsync(academicMediumId, input.RegistrationNo, vehicleId);

		vehicle.UpdateDetails(
			input.RegistrationNo,
			input.VehicleNo,
			input.VehicleName,
			input.VehicleTypeId,
			input.VehicleCompany,
			input.Model,
			input.YearOfManufacture,
			input.SeatingCapacity,
			input.VehicleColor,
			input.SchoolAssignedNo,
			input.Owner,
			input.FuelTypeId,
			input.IsAc,
			input.Description);

		await vehicleRepository.UpdateAsync(vehicle, autoSave: true);
		return await GetAsync(vehicleId, academicMediumId);
	}

	public async Task<TransportVehicleDto> ChangeStatusAsync(Guid vehicleId, Guid academicMediumId, ChangeTransportVehicleStatusDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(ChangeStatusAsync));
		var vehicle = await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		if (input.Status != TransportVehicleStatus.Active)
		{
			var currentAssignment = await FindCurrentAssignmentForVehicleAsync(vehicleId, academicMediumId);
			if (currentAssignment != null)
			{
				throw new BusinessException(TransportErrorCodes.RouteAssignmentConflict)
					.WithData("VehicleId", vehicleId)
					.WithData("RouteId", currentAssignment.RouteId);
			}
		}
		vehicle.ChangeStatus(input.Status);
		await vehicleRepository.UpdateAsync(vehicle, autoSave: true);
		return await GetAsync(vehicleId, academicMediumId);
	}

	public async Task<List<TransportOptionDto>> GetOptionsAsync(TransportVehicleOptionsRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetOptionsAsync));

		var query = await vehicleRepository.GetQueryableAsync();
		query = query.Where(vehicle =>
			vehicle.AcademicMediumId == input.AcademicMediumId &&
			vehicle.Status == TransportVehicleStatus.Active);

		if (input.OnlyAssignable)
		{
			var assignedVehicleIds = await GetAssignedVehicleIdsAsync(input.AcademicMediumId);
			query = query.Where(vehicle => !assignedVehicleIds.Contains(vehicle.Id));
		}

		var vehicles = await AsyncExecuter.ToListAsync(query.OrderBy(vehicle => vehicle.RegistrationNo));
		return vehicles.Select(vehicle =>
		{
			var name = vehicle.VehicleName ?? vehicle.VehicleNo;
			var label = string.IsNullOrWhiteSpace(name)
				? vehicle.RegistrationNo
				: $"{vehicle.RegistrationNo} - {name}";
			return new TransportOptionDto
			{
				Id = vehicle.Id,
				Label = label,
				Value = vehicle.Id.ToString("D"),
				Name = name,
				Code = vehicle.RegistrationNo,
				Description = $"{vehicle.SeatingCapacity} seats"
			};
		}).ToList();
	}

	public async Task<List<TransportVehicleDocumentDto>> GetDocumentsAsync(Guid vehicleId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(GetDocumentsAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		var requiredDocuments = await requiredDocumentRepository.GetListAsync(x => x.AcademicMediumId == academicMediumId && x.IsActive);
		var uploaded = await vehicleDocumentRepository.GetListAsync(x => x.VehicleId == vehicleId && x.AcademicMediumId == academicMediumId);
		var uploadedByRequiredId = uploaded.ToDictionary(x => x.RequiredDocumentId, x => x);

		return requiredDocuments
			.OrderBy(x => x.Name)
			.Select(required =>
			{
				uploadedByRequiredId.TryGetValue(required.Id, out var document);
				return MapVehicleDocument(required, document);
			})
			.ToList();
	}

	public async Task<TransportVehicleDocumentDto> UploadDocumentAsync(Guid vehicleId, Guid academicMediumId, TransportVehicleDocumentUploadDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UploadDocumentAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		var required = await GetRequiredDocumentForMediumAsync(input.RequiredDocumentId, academicMediumId);
		ValidateVehicleDocument(required, input);

		var existing = await vehicleDocumentRepository.FindAsync(x =>
			x.VehicleId == vehicleId &&
			x.AcademicMediumId == academicMediumId &&
			x.RequiredDocumentId == input.RequiredDocumentId);

		if (existing != null)
		{
			return await UpdateDocumentAsync(vehicleId, existing.Id, academicMediumId, input);
		}

		var documentId = GuidGenerator.Create();
		var blobName = BuildVehicleDocumentBlobName(vehicleId, documentId, input.FileName);
		await blobContainer.SaveAsync(blobName, input.Content, overrideExisting: true);

		var document = new TransportVehicleDocument(
			documentId,
			CurrentTenant.Id,
			academicMediumId,
			vehicleId,
			input.RequiredDocumentId,
			input.FileName,
			input.ContentType,
			input.Size,
			blobName);

		await vehicleDocumentRepository.InsertAsync(document, autoSave: true);
		return MapVehicleDocument(required, document);
	}

	public async Task<TransportVehicleDocumentDto> UpdateDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId, TransportVehicleDocumentUploadDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(UpdateDocumentAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		var required = await GetRequiredDocumentForMediumAsync(input.RequiredDocumentId, academicMediumId);
		ValidateVehicleDocument(required, input);
		var document = await GetVehicleDocumentAsync(vehicleId, documentId, academicMediumId);

		await blobContainer.SaveAsync(document.BlobName, input.Content, overrideExisting: true);
		document.UpdateFile(input.FileName, input.ContentType, input.Size, document.BlobName);
		await vehicleDocumentRepository.UpdateAsync(document, autoSave: true);
		return MapVehicleDocument(required, document);
	}

	public async Task<TransportVehicleDocumentDownloadDto> DownloadDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DownloadDocumentAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		var document = await GetVehicleDocumentAsync(vehicleId, documentId, academicMediumId);

		await using var stream = await blobContainer.GetAsync(document.BlobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return new TransportVehicleDocumentDownloadDto
		{
			FileName = document.FileName,
			ContentType = document.ContentType,
			Content = memoryStream.ToArray()
		};
	}

	public async Task DeleteDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(DeleteDocumentAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		var document = await GetVehicleDocumentAsync(vehicleId, documentId, academicMediumId);
		await blobContainer.DeleteAsync(document.BlobName);
		await vehicleDocumentRepository.DeleteAsync(document, autoSave: true);
	}

	public async Task<TransportVehicleComplianceDto> SaveComplianceAsync(Guid vehicleId, Guid academicMediumId, TransportVehicleComplianceDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(SaveComplianceAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		ValidateCompliance(input);

		var compliance = await complianceRepository.FindAsync(x => x.VehicleId == vehicleId && x.AcademicMediumId == academicMediumId);
		var isNew = compliance == null;
		compliance ??= new TransportVehicleCompliance(GuidGenerator.Create(), CurrentTenant.Id, academicMediumId, vehicleId);

		compliance.UpdateDetails(
			input.InsuranceCompany,
			input.PolicyNo,
			input.InsuranceIssueDate,
			input.InsuranceExpiryDate,
			input.CoverageAmount,
			input.PucCertificateNo,
			input.PucIssueDate,
			input.PucExpiryDate,
			input.FitnessCertificateNo,
			input.FitnessIssueDate,
			input.FitnessExpiryDate,
			input.RoadTaxReceiptNo,
			input.RoadTaxIssueDate);

		if (isNew)
		{
			await complianceRepository.InsertAsync(compliance, autoSave: true);
		}
		else
		{
			await complianceRepository.UpdateAsync(compliance, autoSave: true);
		}

		return MapCompliance(compliance);
	}

	public async Task<PagedResultDto<TransportVehicleMaintenanceRecordDto>> GetMaintenanceRecordsAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetMaintenanceRecordsAsync));
		await GetVehicleForMediumAsync(vehicleId, input.AcademicMediumId);

		var query = await maintenanceRepository.GetQueryableAsync();
		query = query.Where(x => x.VehicleId == vehicleId && x.AcademicMediumId == input.AcademicMediumId);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.ServiceDate)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportVehicleMaintenanceRecordDto>(totalCount, rows.Select(MapMaintenanceRecord).ToList());
	}

	public async Task<TransportVehicleMaintenanceRecordDto> AddMaintenanceRecordAsync(Guid vehicleId, Guid academicMediumId, CreateTransportVehicleMaintenanceRecordDto input)
	{
		await EnsureCanUseMediumAsync(academicMediumId, nameof(AddMaintenanceRecordAsync));
		await GetVehicleForMediumAsync(vehicleId, academicMediumId);
		ValidateMaintenance(input);

		var photoBlobNames = new List<string>();
		foreach (var photo in input.Photos)
		{
			FileValidationService.ValidateProfilePictureUpload(photo.FileName, photo.Content);
			var blobName = BuildVehicleMaintenancePhotoBlobName(vehicleId, photo.FileName);
			await blobContainer.SaveAsync(blobName, photo.Content, overrideExisting: true);
			photoBlobNames.Add(blobName);
		}

		var record = new TransportVehicleMaintenanceRecord(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicMediumId,
			vehicleId,
			input.ServiceType,
			input.ServiceDate,
			input.NextServiceDate,
			input.Cost,
			input.Provider,
			input.Notes,
			photoBlobNames.Count == 0 ? null : string.Join("|", photoBlobNames));

		await maintenanceRepository.InsertAsync(record, autoSave: true);
		return MapMaintenanceRecord(record);
	}

	public async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetRouteHistoryAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetRouteHistoryAsync));
		await GetVehicleForMediumAsync(vehicleId, input.AcademicMediumId);
		return await GetVehicleAssignmentHistoryAsync(vehicleId, input);
	}

	public async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetDriverHistoryAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input)
	{
		await EnsureCanUseMediumAsync(input.AcademicMediumId, nameof(GetDriverHistoryAsync));
		await GetVehicleForMediumAsync(vehicleId, input.AcademicMediumId);
		return await GetVehicleAssignmentHistoryAsync(vehicleId, input);
	}

	private async Task<TransportVehicle> GetVehicleForMediumAsync(Guid vehicleId, Guid academicMediumId)
	{
		var vehicle = await vehicleRepository.FindAsync(vehicleId);
		if (vehicle == null || vehicle.AcademicMediumId != academicMediumId)
		{
			throw new EntityNotFoundException(typeof(TransportVehicle), vehicleId);
		}
		return vehicle;
	}

	private async Task EnsureCanUseMediumAsync(Guid academicMediumId, string operation)
	{
		await academicMediumRepository.GetAsync(academicMediumId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(academicMediumId, operation);
	}

	private async Task EnsureRegistrationIsUniqueAsync(Guid academicMediumId, string registrationNo, Guid? excludingVehicleId = null)
	{
		var normalized = NormalizeCode(registrationNo);
		var rows = await vehicleRepository.GetListAsync(x =>
			x.AcademicMediumId == academicMediumId &&
			(!excludingVehicleId.HasValue || x.Id != excludingVehicleId.Value));

		if (rows.Any(x => NormalizeCode(x.RegistrationNo) == normalized))
		{
			throw new BusinessException(TransportErrorCodes.VehicleAlreadyExists)
				.WithData("RegistrationNo", normalized)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private async Task EnsureVehicleTypeExistsAsync(Guid academicMediumId, Guid vehicleTypeId)
	{
		var row = await vehicleTypeRepository.FindAsync(vehicleTypeId);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new BusinessException(TransportErrorCodes.VehicleTypeNotFound).WithData("VehicleTypeId", vehicleTypeId);
		}
	}

	private async Task EnsureFuelTypeExistsAsync(Guid academicMediumId, Guid fuelTypeId)
	{
		var row = await fuelTypeRepository.FindAsync(fuelTypeId);
		if (row == null || row.AcademicMediumId != academicMediumId)
		{
			throw new BusinessException(TransportErrorCodes.FuelTypeNotFound).WithData("FuelTypeId", fuelTypeId);
		}
	}

	private async Task<TransportRequiredVehicleDocument> GetRequiredDocumentForMediumAsync(Guid requiredDocumentId, Guid academicMediumId)
	{
		var required = await requiredDocumentRepository.FindAsync(requiredDocumentId);
		if (required == null || required.AcademicMediumId != academicMediumId)
		{
			throw new BusinessException(TransportErrorCodes.RequiredVehicleDocumentNotFound).WithData("RequiredDocumentId", requiredDocumentId);
		}
		return required;
	}

	private async Task<TransportVehicleDocument> GetVehicleDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId)
	{
		var document = await vehicleDocumentRepository.FindAsync(documentId);
		if (document == null || document.VehicleId != vehicleId || document.AcademicMediumId != academicMediumId)
		{
			throw new BusinessException(TransportErrorCodes.VehicleDocumentNotFound).WithData("DocumentId", documentId);
		}
		return document;
	}

	private async Task<List<TransportVehicleDto>> MapVehiclesAsync(List<TransportVehicle> vehicles)
	{
		var vehicleTypeIds = vehicles.Select(x => x.VehicleTypeId).Distinct().ToList();
		var fuelTypeIds = vehicles.Select(x => x.FuelTypeId).Distinct().ToList();
		var complianceVehicleIds = vehicles.Select(x => x.Id).ToList();

		var vehicleTypes = (await vehicleTypeRepository.GetListAsync(x => vehicleTypeIds.Contains(x.Id)))
			.ToDictionary(x => x.Id, x => x.Name);
		var fuelTypes = (await fuelTypeRepository.GetListAsync(x => fuelTypeIds.Contains(x.Id)))
			.ToDictionary(x => x.Id, x => x.Name);
		var compliance = (await complianceRepository.GetListAsync(x => complianceVehicleIds.Contains(x.VehicleId)))
			.ToDictionary(x => x.VehicleId, x => x);
		var assignments = await GetCurrentAssignmentsForVehiclesAsync(complianceVehicleIds);

		return vehicles.Select(vehicle =>
		{
			vehicleTypes.TryGetValue(vehicle.VehicleTypeId, out var vehicleType);
			fuelTypes.TryGetValue(vehicle.FuelTypeId, out var fuelType);
			compliance.TryGetValue(vehicle.Id, out var complianceRow);
			assignments.TryGetValue(vehicle.Id, out var assignment);
			var complianceDto = complianceRow == null ? null : MapCompliance(complianceRow);

			return new TransportVehicleDto
			{
				Id = vehicle.Id,
				AcademicMediumId = vehicle.AcademicMediumId,
				RegistrationNo = vehicle.RegistrationNo,
				RegNo = vehicle.RegistrationNo,
				VehicleNo = vehicle.VehicleNo,
				VehicleName = vehicle.VehicleName,
				VehicleTypeId = vehicle.VehicleTypeId,
				VehicleType = vehicleType ?? string.Empty,
				VehicleCompany = vehicle.VehicleCompany,
				Model = vehicle.Model,
				YearOfManufacture = vehicle.YearOfManufacture,
				SeatingCapacity = vehicle.SeatingCapacity,
				Capacity = vehicle.SeatingCapacity,
				VehicleColor = vehicle.VehicleColor,
				SchoolAssignedNo = vehicle.SchoolAssignedNo,
				SchoolAssignedNumber = vehicle.SchoolAssignedNo,
				Owner = vehicle.Owner,
				FuelTypeId = vehicle.FuelTypeId,
				FuelType = fuelType ?? string.Empty,
				IsAc = vehicle.IsAc,
				IsNonAc = !vehicle.IsAc,
				Description = vehicle.Description,
				Status = vehicle.Status,
				StatusName = vehicle.Status.ToString(),
				Route = assignment?.RouteName,
				Driver = assignment?.DriverName,
				ConductorName = assignment?.ConductorName,
				InsuranceExpiry = complianceRow?.InsuranceExpiryDate?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
				Compliance = complianceDto,
				CreationTime = vehicle.CreationTime,
				CreatorId = vehicle.CreatorId,
				LastModificationTime = vehicle.LastModificationTime,
				LastModifierId = vehicle.LastModifierId
			};
		}).ToList();
	}

	private async Task<TransportRouteAssignment?> FindCurrentAssignmentForVehicleAsync(Guid vehicleId, Guid academicMediumId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.VehicleId == vehicleId &&
			assignment.EffectiveTo == null));
	}

	private async Task<HashSet<Guid>> GetAssignedVehicleIdsAsync(Guid academicMediumId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(assignment =>
			assignment.AcademicMediumId == academicMediumId &&
			assignment.EffectiveTo == null));

		return rows.Select(assignment => assignment.VehicleId).ToHashSet();
	}

	private async Task<IReadOnlyDictionary<Guid, TransportVehicleCurrentAssignment>> GetCurrentAssignmentsForVehiclesAsync(List<Guid> vehicleIds)
	{
		if (vehicleIds.Count == 0)
		{
			return new Dictionary<Guid, TransportVehicleCurrentAssignment>();
		}

		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var routeQuery = await routeRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			from assignment in assignmentQuery
			join route in routeQuery on assignment.RouteId equals route.Id
			where assignment.EffectiveTo == null && vehicleIds.Contains(assignment.VehicleId)
			select new { Assignment = assignment, Route = route });

		var staffIds = rows
			.SelectMany(row => new[] { row.Assignment.DriverStaffId, row.Assignment.ConductorStaffId ?? Guid.Empty })
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();
		var staff = (await staffRepository.GetListAsync(x => staffIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var employeeIds = staff.Values.Select(x => x.EmployeeId).Distinct().ToList();
		var employees = (await employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id))).ToDictionary(x => x.Id);

		return rows.ToDictionary(
			row => row.Assignment.VehicleId,
			row =>
			{
				var driverName = GetStaffEmployeeName(row.Assignment.DriverStaffId, staff, employees);
				var conductorName = row.Assignment.ConductorStaffId.HasValue
					? GetStaffEmployeeName(row.Assignment.ConductorStaffId.Value, staff, employees)
					: null;

				return new TransportVehicleCurrentAssignment(row.Route.RouteName, driverName, conductorName);
			});
	}

	private async Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetVehicleAssignmentHistoryAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		query = query.Where(assignment =>
			assignment.AcademicMediumId == input.AcademicMediumId &&
			assignment.VehicleId == vehicleId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var assignments = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(assignment => assignment.EffectiveFrom)
				.Skip(input.SkipCount)
				.Take(input.NormalizedPageSize));

		return new PagedResultDto<TransportVehicleHistoryRowDto>(
			totalCount,
			await MapVehicleHistoryRowsAsync(assignments));
	}

	private async Task<List<TransportVehicleHistoryRowDto>> MapVehicleHistoryRowsAsync(List<TransportRouteAssignment> assignments)
	{
		if (assignments.Count == 0)
		{
			return new List<TransportVehicleHistoryRowDto>();
		}

		var routeIds = assignments.Select(x => x.RouteId).Distinct().ToList();
		var vehicleIds = assignments.Select(x => x.VehicleId).Distinct().ToList();
		var staffIds = assignments
			.SelectMany(x => new[] { x.DriverStaffId, x.ConductorStaffId ?? Guid.Empty })
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();

		var routes = (await routeRepository.GetListAsync(x => routeIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var vehicles = (await vehicleRepository.GetListAsync(x => vehicleIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var staff = (await staffRepository.GetListAsync(x => staffIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var employeeIds = staff.Values.Select(x => x.EmployeeId).Distinct().ToList();
		var employees = (await employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id))).ToDictionary(x => x.Id);

		return assignments.Select(assignment =>
		{
			routes.TryGetValue(assignment.RouteId, out var route);
			vehicles.TryGetValue(assignment.VehicleId, out var vehicle);
			var driverName = GetStaffEmployeeName(assignment.DriverStaffId, staff, employees);
			var conductorName = assignment.ConductorStaffId.HasValue
				? GetStaffEmployeeName(assignment.ConductorStaffId.Value, staff, employees)
				: null;

			return new TransportVehicleHistoryRowDto
			{
				Id = assignment.Id,
				Name = route?.RouteName ?? string.Empty,
				RouteId = assignment.RouteId,
				RouteName = route?.RouteName ?? string.Empty,
				RouteCode = route?.RouteCode ?? string.Empty,
				VehicleId = assignment.VehicleId,
				VehicleName = vehicle?.VehicleName ?? vehicle?.VehicleNo ?? vehicle?.RegistrationNo ?? string.Empty,
				VehicleRegistrationNo = vehicle?.RegistrationNo ?? string.Empty,
				DriverStaffId = assignment.DriverStaffId,
				DriverName = driverName,
				ConductorStaffId = assignment.ConductorStaffId,
				ConductorName = conductorName,
				EffectiveFrom = assignment.EffectiveFrom,
				EffectiveTo = assignment.EffectiveTo,
				IsCurrent = assignment.EffectiveTo == null
			};
		}).ToList();
	}

	private static string GetStaffEmployeeName(Guid staffId, IReadOnlyDictionary<Guid, TransportStaff> staff, IReadOnlyDictionary<Guid, Employee> employees)
	{
		if (!staff.TryGetValue(staffId, out var staffRow) || !employees.TryGetValue(staffRow.EmployeeId, out var employee))
		{
			return string.Empty;
		}

		var name = employee.EmployeeName;
		return string.IsNullOrWhiteSpace(name) ? employee.EmployeeCode : name;
	}

	private static TransportVehicleDocumentDto MapVehicleDocument(TransportRequiredVehicleDocument required, TransportVehicleDocument? document)
	{
		var acceptedFormats = ParseAcceptedFormats(required.AcceptedFormatsCsv);
		return new TransportVehicleDocumentDto
		{
			Id = document?.Id ?? Guid.Empty,
			RequiredDocumentId = required.Id,
			Name = required.Name,
			Status = document == null ? "Pending" : "Uploaded",
			FileName = document?.FileName,
			ContentType = document?.ContentType,
			Size = document?.Size,
			MaxSizeMb = required.MaxSizeMb,
			AcceptedFormats = acceptedFormats,
			IsRequired = required.IsRequired,
			CreationTime = document?.CreationTime ?? default,
			CreatorId = document?.CreatorId,
			LastModificationTime = document?.LastModificationTime,
			LastModifierId = document?.LastModifierId
		};
	}

	private static TransportVehicleComplianceDto MapCompliance(TransportVehicleCompliance compliance) => new()
	{
		InsuranceCompany = compliance.InsuranceCompany,
		PolicyNo = compliance.PolicyNo,
		InsuranceIssueDate = compliance.InsuranceIssueDate,
		InsuranceExpiryDate = compliance.InsuranceExpiryDate,
		CoverageAmount = compliance.CoverageAmount,
		PucCertificateNo = compliance.PucCertificateNo,
		PucIssueDate = compliance.PucIssueDate,
		PucExpiryDate = compliance.PucExpiryDate,
		FitnessCertificateNo = compliance.FitnessCertificateNo,
		FitnessIssueDate = compliance.FitnessIssueDate,
		FitnessExpiryDate = compliance.FitnessExpiryDate,
		RoadTaxReceiptNo = compliance.RoadTaxReceiptNo,
		RoadTaxIssueDate = compliance.RoadTaxIssueDate
	};

	private static TransportVehicleMaintenanceRecordDto MapMaintenanceRecord(TransportVehicleMaintenanceRecord row) => new()
	{
		Id = row.Id,
		Date = row.ServiceDate,
		ServiceType = row.ServiceType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
		Cost = row.Cost ?? 0,
		NextDue = row.NextServiceDate,
		ServiceProvider = row.Provider,
		Description = row.Notes,
		CreationTime = row.CreationTime,
		CreatorId = row.CreatorId,
		LastModificationTime = row.LastModificationTime,
		LastModifierId = row.LastModifierId
	};

	private static void ValidateVehicleInput(CreateUpdateTransportVehicleDto input)
	{
		if (string.IsNullOrWhiteSpace(input.RegistrationNo))
		{
			throw new UserFriendlyException("Registration no. is required.");
		}
		if (string.IsNullOrWhiteSpace(input.VehicleNo))
		{
			throw new UserFriendlyException("Vehicle no. is required.");
		}
		if (input.VehicleTypeId == Guid.Empty)
		{
			throw new UserFriendlyException("Vehicle type is required.");
		}
		if (input.FuelTypeId == Guid.Empty)
		{
			throw new UserFriendlyException("Fuel type is required.");
		}
		if (input.SeatingCapacity <= 0)
		{
			throw new UserFriendlyException("Seating capacity must be greater than zero.");
		}
	}

	private static void ValidateVehicleDocument(TransportRequiredVehicleDocument required, TransportVehicleDocumentUploadDto input)
	{
		if (input.RequiredDocumentId == Guid.Empty || input.RequiredDocumentId != required.Id)
		{
			throw new UserFriendlyException("Required document id is invalid.");
		}
		if (input.Size > (long)required.MaxSizeMb * 1024 * 1024)
		{
			throw new UserFriendlyException($"File size cannot exceed {required.MaxSizeMb} MB.");
		}

		FileValidationService.ValidateDocumentUpload(input.FileName, input.Content, input.Size);
	}

	private static void ValidateCompliance(TransportVehicleComplianceDto input)
	{
		if (input.CoverageAmount.HasValue && input.CoverageAmount.Value < 0)
		{
			throw new UserFriendlyException("Coverage amount cannot be negative.");
		}
		if (input.InsuranceIssueDate.HasValue && input.InsuranceExpiryDate.HasValue && input.InsuranceExpiryDate.Value.Date < input.InsuranceIssueDate.Value.Date)
		{
			throw new UserFriendlyException("Insurance expiry date cannot be before issue date.");
		}
		if (input.PucIssueDate.HasValue && input.PucExpiryDate.HasValue && input.PucExpiryDate.Value.Date < input.PucIssueDate.Value.Date)
		{
			throw new UserFriendlyException("PUC expiry date cannot be before issue date.");
		}
		if (input.FitnessIssueDate.HasValue && input.FitnessExpiryDate.HasValue && input.FitnessExpiryDate.Value.Date < input.FitnessIssueDate.Value.Date)
		{
			throw new UserFriendlyException("Fitness expiry date cannot be before issue date.");
		}
	}

	private static void ValidateMaintenance(CreateTransportVehicleMaintenanceRecordDto input)
	{
		if (string.IsNullOrWhiteSpace(input.ServiceType))
		{
			throw new UserFriendlyException("Service type is required.");
		}
		if (input.ServiceDate == default)
		{
			throw new UserFriendlyException("Service date is required.");
		}
		if (input.Cost.HasValue && input.Cost.Value < 0)
		{
			throw new UserFriendlyException("Service cost cannot be negative.");
		}
	}

	private static string BuildVehicleDocumentBlobName(Guid vehicleId, Guid documentId, string fileName) =>
		$"transport-vehicle-documents/{vehicleId}/{documentId}_{Path.GetFileName(fileName)}";

	private static string BuildVehicleMaintenancePhotoBlobName(Guid vehicleId, string fileName) =>
		$"transport-vehicle-maintenance/{vehicleId}/{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";

	private static string NormalizeCode(string value) =>
		string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

	private static List<int> ParseAcceptedFormats(string csv) =>
		csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(part => int.TryParse(part.Trim(), out var parsed) ? parsed : (int?)null)
			.Where(parsed => parsed.HasValue)
			.Select(parsed => parsed!.Value)
			.ToList();

	private sealed record TransportVehicleCurrentAssignment(string RouteName, string DriverName, string? ConductorName);
}
