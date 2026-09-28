using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionClassRoomDto
{
	public Guid RoomId { get; set; }
	public string RoomName { get; set; } = string.Empty;
	public int Capacity { get; set; }
}

public class AdmissionRoomChoiceDto : AdmissionClassRoomDto
{
	public bool IsAllocatedToAnotherClass { get; set; }
}

public class AdmissionClassCapacityDto
{
	public Guid AdmissionCycleId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public List<AdmissionClassRoomDto> Rooms { get; set; } = [];
	public int? Capacity { get; set; }
	public int OccupiedCount { get; set; }
	public int? RemainingSeats { get; set; }
	public bool IsFull { get; set; }
	public bool IsOverCapacity { get; set; }
	public string? Warning { get; set; }
}

public class UpdateAdmissionClassRoomsDto
{
	public List<Guid> RoomIds { get; set; } = [];
}

public interface IAdmissionClassCapacityAppService : IApplicationService
{
	Task<AdmissionClassCapacityDto> GetForOfferingAsync(Guid offeringId);
	Task<List<AdmissionRoomChoiceDto>> GetRoomChoicesAsync(Guid offeringId, string? search);
	Task<AdmissionClassCapacityDto> UpdateForOfferingAsync(Guid offeringId, UpdateAdmissionClassRoomsDto input);
}
