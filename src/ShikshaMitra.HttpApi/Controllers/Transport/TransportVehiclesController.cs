using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Models;
using ShikshaMitra.Transport;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Transport;

[ApiController]
[Authorize]
[Route("api/transport/vehicles")]
public class TransportVehiclesController(ITransportVehicleAppService vehicleAppService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleDto>>> GetVehicles([FromQuery] TransportVehicleListRequestDto input)
	{
		var vehicles = await vehicleAppService.GetListAsync(input);
		return Result.Success(vehicles);
	}

	[HttpGet("options")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<List<TransportOptionDto>>> GetVehicleOptions([FromQuery] TransportVehicleOptionsRequestDto input)
	{
		var options = await vehicleAppService.GetOptionsAsync(input);
		return Result.Success(options);
	}

	[HttpGet("{vehicleId}")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDto>> GetVehicle(Guid vehicleId, [FromQuery] Guid academicMediumId)
	{
		var vehicle = await vehicleAppService.GetAsync(vehicleId, academicMediumId);
		return Result.Success(vehicle);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDto>> CreateVehicle(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportVehicleDto input)
	{
		var vehicle = await vehicleAppService.CreateAsync(academicMediumId, input);
		return Result.Success(vehicle);
	}

	[HttpPut("{vehicleId}")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDto>> UpdateVehicle(
		Guid vehicleId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportVehicleDto input)
	{
		var vehicle = await vehicleAppService.UpdateAsync(vehicleId, academicMediumId, input);
		return Result.Success(vehicle);
	}

	[HttpPut("{vehicleId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDto>> ChangeVehicleStatus(
		Guid vehicleId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportVehicleStatusDto input)
	{
		var vehicle = await vehicleAppService.ChangeStatusAsync(vehicleId, academicMediumId, input);
		return Result.Success(vehicle);
	}

	[HttpGet("{vehicleId}/documents")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<List<TransportVehicleDocumentDto>>> GetDocuments(Guid vehicleId, [FromQuery] Guid academicMediumId)
	{
		var documents = await vehicleAppService.GetDocumentsAsync(vehicleId, academicMediumId);
		return Result.Success(documents);
	}

	[HttpPost("{vehicleId}/documents")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDocumentDto>> UploadDocument(
		Guid vehicleId,
		[FromQuery] Guid academicMediumId,
		[FromForm] TransportVehicleDocumentUploadRequest request)
	{
		var document = await vehicleAppService.UploadDocumentAsync(
			vehicleId,
			academicMediumId,
			await ToDocumentUploadDtoAsync(request));
		return Result.Success(document);
	}

	[HttpPut("{vehicleId}/documents/{documentId}")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleDocumentDto>> ReplaceDocument(
		Guid vehicleId,
		Guid documentId,
		[FromQuery] Guid academicMediumId,
		[FromForm] TransportVehicleDocumentUploadRequest request)
	{
		var document = await vehicleAppService.UpdateDocumentAsync(
			vehicleId,
			documentId,
			academicMediumId,
			await ToDocumentUploadDtoAsync(request));
		return Result.Success(document);
	}

	[HttpGet("{vehicleId}/documents/{documentId}")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<IActionResult> DownloadDocument(Guid vehicleId, Guid documentId, [FromQuery] Guid academicMediumId)
	{
		var document = await vehicleAppService.DownloadDocumentAsync(vehicleId, documentId, academicMediumId);
		return File(document.Content, document.ContentType, document.FileName);
	}

	[HttpDelete("{vehicleId}/documents/{documentId}")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<IActionResult> DeleteDocument(Guid vehicleId, Guid documentId, [FromQuery] Guid academicMediumId)
	{
		await vehicleAppService.DeleteDocumentAsync(vehicleId, documentId, academicMediumId);
		return NoContent();
	}

	[HttpPut("{vehicleId}/compliance")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleComplianceDto>> SaveCompliance(
		Guid vehicleId,
		[FromQuery] Guid academicMediumId,
		[FromBody] TransportVehicleComplianceDto input)
	{
		var compliance = await vehicleAppService.SaveComplianceAsync(vehicleId, academicMediumId, input);
		return Result.Success(compliance);
	}

	[HttpGet("{vehicleId}/maintenance-records")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleMaintenanceRecordDto>>> GetMaintenanceRecords(
		Guid vehicleId,
		[FromQuery] TransportVehicleMaintenanceListRequestDto input)
	{
		var rows = await vehicleAppService.GetMaintenanceRecordsAsync(vehicleId, input);
		return Result.Success(rows);
	}

	[HttpPost("{vehicleId}/maintenance-records")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<TransportVehicleMaintenanceRecordDto>> AddMaintenanceRecord(
		Guid vehicleId,
		[FromQuery] Guid academicMediumId,
		[FromForm] TransportVehicleMaintenanceRecordRequest request)
	{
		var record = await vehicleAppService.AddMaintenanceRecordAsync(
			vehicleId,
			academicMediumId,
			await ToMaintenanceDtoAsync(request));
		return Result.Success(record);
	}

	[HttpGet("{vehicleId}/route-history")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleHistoryRowDto>>> GetRouteHistory(
		Guid vehicleId,
		[FromQuery] TransportVehicleMaintenanceListRequestDto input)
	{
		var rows = await vehicleAppService.GetRouteHistoryAsync(vehicleId, input);
		return Result.Success(rows);
	}

	[HttpGet("{vehicleId}/driver-history")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleHistoryRowDto>>> GetDriverHistory(
		Guid vehicleId,
		[FromQuery] TransportVehicleMaintenanceListRequestDto input)
	{
		var rows = await vehicleAppService.GetDriverHistoryAsync(vehicleId, input);
		return Result.Success(rows);
	}

	private static async Task<TransportVehicleDocumentUploadDto> ToDocumentUploadDtoAsync(TransportVehicleDocumentUploadRequest request)
	{
		if (request.File == null || request.File.Length == 0)
		{
			throw new UserFriendlyException("A vehicle document file is required.");
		}

		await using var memoryStream = new MemoryStream();
		await request.File.CopyToAsync(memoryStream);
		return new TransportVehicleDocumentUploadDto
		{
			RequiredDocumentId = request.RequiredDocumentId,
			FileName = request.FileName ?? request.File.FileName,
			ContentType = request.File.ContentType,
			Size = request.File.Length,
			Content = memoryStream.ToArray()
		};
	}

	private static async Task<CreateTransportVehicleMaintenanceRecordDto> ToMaintenanceDtoAsync(TransportVehicleMaintenanceRecordRequest request)
	{
		var photos = new List<TransportMaintenancePhotoDto>();
		foreach (var photo in request.Photos.Where(photo => photo.Length > 0))
		{
			await using var memoryStream = new MemoryStream();
			await photo.CopyToAsync(memoryStream);
			photos.Add(new TransportMaintenancePhotoDto
			{
				FileName = photo.FileName,
				ContentType = photo.ContentType,
				Size = photo.Length,
				Content = memoryStream.ToArray()
			});
		}

		return new CreateTransportVehicleMaintenanceRecordDto
		{
			ServiceType = request.ServiceType,
			ServiceDate = request.ServiceDate,
			NextServiceDate = request.NextServiceDate,
			Cost = request.Cost,
			Provider = request.Provider,
			Notes = request.Notes,
			Photos = photos
		};
	}
}
