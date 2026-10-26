using System;
using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public interface IBuildingRoomCapacityLock
{
	Task AcquireAsync(Guid buildingId);
}
