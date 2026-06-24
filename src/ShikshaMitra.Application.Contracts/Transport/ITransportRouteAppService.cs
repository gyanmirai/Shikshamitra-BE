using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportRouteAppService
{
	Task<PagedResultDto<TransportRouteDto>> GetListAsync(TransportRouteListRequestDto input);
	Task<TransportRouteDto> GetAsync(Guid routeId, Guid academicMediumId);
	Task<TransportRouteDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportRouteDto input);
	Task<TransportRouteDto> UpdateAsync(Guid routeId, Guid academicMediumId, CreateUpdateTransportRouteDto input);
	Task<List<TransportRouteStopDto>> GetStopsAsync(Guid routeId, Guid academicMediumId);
	Task<List<TransportRouteStopDto>> AssignStopsAsync(Guid routeId, Guid academicMediumId, AssignTransportRouteStopsDto input);
	Task<TransportRouteDto> ChangeStatusAsync(Guid routeId, Guid academicMediumId, ChangeTransportStatusDto input);
	Task<TransportRouteAssignmentDto?> GetVehicleAssignmentAsync(Guid routeId, Guid academicMediumId);
	Task<TransportRouteAssignmentDto> AssignVehicleAsync(Guid routeId, Guid academicMediumId, AssignTransportRouteVehicleDto input);
	Task DeleteVehicleAssignmentAsync(Guid routeId, Guid academicMediumId);
	Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetVehicleHistoryAsync(Guid routeId, TransportVehicleMaintenanceListRequestDto input);
	Task<PagedResultDto<TransportVehicleHistoryRowDto>> GetDriverConductorHistoryAsync(Guid routeId, TransportVehicleMaintenanceListRequestDto input);
}
