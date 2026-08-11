using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicExamSlotDto
{
	public Guid EntranceExamSlotId { get; set; }
	public string SlotName { get; set; } = string.Empty;
	public DateTimeOffset StartTime { get; set; }
	public DateTimeOffset EndTime { get; set; }
	public int RoomNo { get; set; }
	public int AvailableSeats { get; set; }
}

public class AdmissionCrmPublicExamSlotListDto
{
	public bool SelfSchedulingEnabled { get; set; }
	public IReadOnlyList<AdmissionCrmPublicExamSlotDto> Slots { get; set; } = [];
}

public class ScheduleAdmissionCrmPublicExamDto
{
	[JsonIgnore]
	public string AccessSessionToken { get; set; } = string.Empty;
	public Guid EntranceExamSlotId { get; set; }
}
