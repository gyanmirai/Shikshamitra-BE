using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Transport;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Transport;

[ApiController]
[Authorize]
public class TransportVehicleSettingsController(ITransportVehicleSettingsAppService settingsAppService) : AbpController
{
	[HttpGet("api/transport/vehicle-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleType.View)]
	public async Task<Result<List<TransportVehicleTypeDto>>> GetVehicleTypes([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetVehicleTypesAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpPost("api/transport/vehicle-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleType.Add)]
	public async Task<Result<TransportVehicleTypeDto>> CreateVehicleType(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportVehicleTypeDto input)
	{
		var row = await settingsAppService.CreateVehicleTypeAsync(academicMediumId, input);
		return Result.Success(row);
	}

	[HttpPut("api/transport/vehicle-types/{vehicleTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleType.Add)]
	public async Task<Result<TransportVehicleTypeDto>> UpdateVehicleType(
		Guid vehicleTypeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportVehicleTypeDto input)
	{
		var row = await settingsAppService.UpdateVehicleTypeAsync(vehicleTypeId, academicMediumId, input);
		return Result.Success(row);
	}

	[HttpDelete("api/transport/vehicle-types/{vehicleTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleType.Delete)]
	public async Task<IActionResult> DeleteVehicleType(Guid vehicleTypeId, [FromQuery] Guid academicMediumId)
	{
		await settingsAppService.DeleteVehicleTypeAsync(vehicleTypeId, academicMediumId);
		return NoContent();
	}

	[HttpGet("api/transport/vehicle-types/options")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<List<TransportOptionDto>>> GetVehicleTypeOptions([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetVehicleTypeOptionsAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpGet("api/transport/required-vehicle-documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleDocument.View)]
	public async Task<Result<List<TransportRequiredVehicleDocumentDto>>> GetRequiredVehicleDocuments([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetRequiredVehicleDocumentsAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpPost("api/transport/required-vehicle-documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleDocument.Add)]
	public async Task<Result<TransportRequiredVehicleDocumentDto>> CreateRequiredVehicleDocument(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportRequiredVehicleDocumentDto input)
	{
		var row = await settingsAppService.CreateRequiredVehicleDocumentAsync(academicMediumId, input);
		return Result.Success(row);
	}

	[HttpPut("api/transport/required-vehicle-documents/{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleDocument.Add)]
	public async Task<Result<TransportRequiredVehicleDocumentDto>> UpdateRequiredVehicleDocument(
		Guid documentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportRequiredVehicleDocumentDto input)
	{
		var row = await settingsAppService.UpdateRequiredVehicleDocumentAsync(documentId, academicMediumId, input);
		return Result.Success(row);
	}

	[HttpDelete("api/transport/required-vehicle-documents/{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_VehicleDocument.Delete)]
	public async Task<IActionResult> DeleteRequiredVehicleDocument(Guid documentId, [FromQuery] Guid academicMediumId)
	{
		await settingsAppService.DeleteRequiredVehicleDocumentAsync(documentId, academicMediumId);
		return NoContent();
	}

	[HttpGet("api/transport/servicing-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ServicingType.View)]
	public async Task<Result<List<TransportServiceTypeDto>>> GetServicingTypes([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetServiceTypesAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpPost("api/transport/servicing-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ServicingType.Add)]
	public async Task<Result<TransportServiceTypeDto>> CreateServicingType(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportServiceTypeDto input)
	{
		var row = await settingsAppService.CreateServiceTypeAsync(academicMediumId, input);
		return Result.Success(row);
	}

	[HttpPut("api/transport/servicing-types/{serviceTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ServicingType.Add)]
	public async Task<Result<TransportServiceTypeDto>> UpdateServicingType(
		Guid serviceTypeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportServiceTypeDto input)
	{
		var row = await settingsAppService.UpdateServiceTypeAsync(serviceTypeId, academicMediumId, input);
		return Result.Success(row);
	}

	[HttpDelete("api/transport/servicing-types/{serviceTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ServicingType.Delete)]
	public async Task<IActionResult> DeleteServicingType(Guid serviceTypeId, [FromQuery] Guid academicMediumId)
	{
		await settingsAppService.DeleteServiceTypeAsync(serviceTypeId, academicMediumId);
		return NoContent();
	}

	[HttpGet("api/transport/servicing-types/options")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<List<TransportOptionDto>>> GetServicingTypeOptions([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetServiceTypeOptionsAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpGet("api/transport/fuel-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FuelType.View)]
	public async Task<Result<List<TransportFuelTypeDto>>> GetFuelTypes([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetFuelTypesAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpPost("api/transport/fuel-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FuelType.Add)]
	public async Task<Result<TransportFuelTypeDto>> CreateFuelType(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportFuelTypeDto input)
	{
		var row = await settingsAppService.CreateFuelTypeAsync(academicMediumId, input);
		return Result.Success(row);
	}

	[HttpPut("api/transport/fuel-types/{fuelTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FuelType.Add)]
	public async Task<Result<TransportFuelTypeDto>> UpdateFuelType(
		Guid fuelTypeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportFuelTypeDto input)
	{
		var row = await settingsAppService.UpdateFuelTypeAsync(fuelTypeId, academicMediumId, input);
		return Result.Success(row);
	}

	[HttpDelete("api/transport/fuel-types/{fuelTypeId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FuelType.Delete)]
	public async Task<IActionResult> DeleteFuelType(Guid fuelTypeId, [FromQuery] Guid academicMediumId)
	{
		await settingsAppService.DeleteFuelTypeAsync(fuelTypeId, academicMediumId);
		return NoContent();
	}

	[HttpGet("api/transport/fuel-types/options")]
	[Authorize(ShikshaMitraSubModulePermissions.VehicleManagement)]
	public async Task<Result<List<TransportOptionDto>>> GetFuelTypeOptions([FromQuery] Guid academicMediumId)
	{
		var rows = await settingsAppService.GetFuelTypeOptionsAsync(academicMediumId);
		return Result.Success(rows);
	}
}
