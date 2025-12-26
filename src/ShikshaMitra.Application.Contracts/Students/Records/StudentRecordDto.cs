using ShikshaMitra.Students.Records.Settings;
using System;

namespace ShikshaMitra.Students.Records;

public class StudentRecordDto
{
	public Guid StudentId { get; set; }
	public required string StudentName { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public Guid? HouseSettingId { get; set; }
	public HouseSettingsDto? HouseSetting { get; set; }
	public bool? IsDivisionHouseLeader { get; set; }
	public bool? IsGlobalHouseLeader { get; set; }
}
