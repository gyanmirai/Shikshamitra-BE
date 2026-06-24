using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Transport;

public interface ITransportVehicleSettingsAppService
{
	Task<List<TransportVehicleTypeDto>> GetVehicleTypesAsync(Guid academicMediumId);
	Task<TransportVehicleTypeDto> CreateVehicleTypeAsync(Guid academicMediumId, CreateUpdateTransportVehicleTypeDto input);
	Task<TransportVehicleTypeDto> UpdateVehicleTypeAsync(Guid vehicleTypeId, Guid academicMediumId, CreateUpdateTransportVehicleTypeDto input);
	Task DeleteVehicleTypeAsync(Guid vehicleTypeId, Guid academicMediumId);

	Task<List<TransportRequiredVehicleDocumentDto>> GetRequiredVehicleDocumentsAsync(Guid academicMediumId);
	Task<TransportRequiredVehicleDocumentDto> CreateRequiredVehicleDocumentAsync(Guid academicMediumId, CreateUpdateTransportRequiredVehicleDocumentDto input);
	Task<TransportRequiredVehicleDocumentDto> UpdateRequiredVehicleDocumentAsync(Guid documentId, Guid academicMediumId, CreateUpdateTransportRequiredVehicleDocumentDto input);
	Task DeleteRequiredVehicleDocumentAsync(Guid documentId, Guid academicMediumId);

	Task<List<TransportServiceTypeDto>> GetServiceTypesAsync(Guid academicMediumId);
	Task<TransportServiceTypeDto> CreateServiceTypeAsync(Guid academicMediumId, CreateUpdateTransportServiceTypeDto input);
	Task<TransportServiceTypeDto> UpdateServiceTypeAsync(Guid serviceTypeId, Guid academicMediumId, CreateUpdateTransportServiceTypeDto input);
	Task DeleteServiceTypeAsync(Guid serviceTypeId, Guid academicMediumId);

	Task<List<TransportFuelTypeDto>> GetFuelTypesAsync(Guid academicMediumId);
	Task<TransportFuelTypeDto> CreateFuelTypeAsync(Guid academicMediumId, CreateUpdateTransportFuelTypeDto input);
	Task<TransportFuelTypeDto> UpdateFuelTypeAsync(Guid fuelTypeId, Guid academicMediumId, CreateUpdateTransportFuelTypeDto input);
	Task DeleteFuelTypeAsync(Guid fuelTypeId, Guid academicMediumId);

	Task<List<TransportOptionDto>> GetVehicleTypeOptionsAsync(Guid academicMediumId);
	Task<List<TransportOptionDto>> GetFuelTypeOptionsAsync(Guid academicMediumId);
	Task<List<TransportOptionDto>> GetServiceTypeOptionsAsync(Guid academicMediumId);
}
