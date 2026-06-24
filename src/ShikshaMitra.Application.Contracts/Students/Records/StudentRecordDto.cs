using ShikshaMitra.Students.Records.Settings;
using System;
using System.Collections.Generic;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public class StudentRecordDto
{
	public Guid StudentId { get; set; }
	public required string StudentName { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
	public StudentStatus? OperationalStatus { get; set; }
	public Guid? HouseSettingId { get; set; }
	public HouseSettingsDto? HouseSetting { get; set; }
	public bool? IsDivisionHouseLeader { get; set; }
	public bool? IsGlobalHouseLeader { get; set; }
}

public class StudentRecordListResponseDto
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public ArrangementTypeEnum? ArrangementType { get; set; }
	public ArrangementByEnum? ArrangementBy { get; set; }
	public List<StudentRecordDto> Items { get; set; } = [];
}
