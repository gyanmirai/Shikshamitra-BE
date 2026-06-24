using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportVehicleAppService
{
	Task<PagedResultDto<TransportVehicleDto>> GetListAsync(TransportVehicleListRequestDto input);
	Task<TransportVehicleDto> GetAsync(Guid vehicleId, Guid academicMediumId);
	Task<TransportVehicleDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportVehicleDto input);
	Task<TransportVehicleDto> UpdateAsync(Guid vehicleId, Guid academicMediumId, CreateUpdateTransportVehicleDto input);
	Task<TransportVehicleDto> ChangeStatusAsync(Guid vehicleId, Guid academicMediumId, ChangeTransportVehicleStatusDto input);
	Task<List<TransportOptionDto>> GetOptionsAsync(TransportVehicleOptionsRequestDto input);

	Task<List<TransportVehicleDocumentDto>> GetDocumentsAsync(Guid vehicleId, Guid academicMediumId);
	Task<TransportVehicleDocumentDto> UploadDocumentAsync(Guid vehicleId, Guid academicMediumId, TransportVehicleDocumentUploadDto input);
	Task<TransportVehicleDocumentDto> UpdateDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId, TransportVehicleDocumentUploadDto input);
	Task<TransportVehicleDocumentDownloadDto> DownloadDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId);
	Task DeleteDocumentAsync(Guid vehicleId, Guid documentId, Guid academicMediumId);

	Task<TransportVehicleComplianceDto> SaveComplianceAsync(Guid vehicleId, Guid academicMediumId, TransportVehicleComplianceDto input);
	Task<PagedResultDto<TransportVehicleMaintenanceRecordDto>> GetMaintenanceRecordsAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input);
	Task<TransportVehicleMaintenanceRecordDto> AddMaintenanceRecordAsync(Guid vehicleId, Guid academicMediumId, CreateTransportVehicleMaintenanceRecordDto input);
	Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetRouteHistoryAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input);
	Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetDriverHistoryAsync(Guid vehicleId, TransportVehicleMaintenanceListRequestDto input);
}
