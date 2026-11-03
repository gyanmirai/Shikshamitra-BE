using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records.HouseManagement;

public class HouseManagementQuery : PagedAndSortedResultRequestDto
{
	public Guid AcademicMediumId { get; set; }

	[StringLength(150)]
	public string? Filter { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? HouseId { get; set; }
	public bool UnassignedOnly { get; set; }
}

public class HouseManagementStudentDto
{
	public Guid EnrollmentId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public string? AdmissionNumber { get; set; }
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = "";
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public int? RollNumber { get; set; }
	public Guid? HouseId { get; set; }
	public string? HouseName { get; set; }
	public bool IsHouseLeader { get; set; }
	public bool IsDivisionLeader { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
}

public class HouseManagementHouseDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public string Color { get; set; } = "";
	public string? LogoUrl { get; set; }
	public string? Description { get; set; }
	public long MemberCount { get; set; }
	public HouseManagementStudentDto? Leader { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public bool CanManage { get; set; }
}

public class HouseManagementDivisionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = "";
	public long StudentCount { get; set; }
	public HouseManagementStudentDto? Leader { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public bool CanManage { get; set; }
}

public class SaveHouseRequest
{
	public Guid AcademicMediumId { get; set; }

	[Required, StringLength(200)]
	public string Name { get; set; } = "";

	[Required, StringLength(50)]
	public string Color { get; set; } = "";

	[StringLength(500)]
	public string? LogoUrl { get; set; }

	[StringLength(1000)]
	public string? Description { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class HouseAssignmentRequest
{
	public Guid AcademicMediumId { get; set; }
	public Guid? HouseId { get; set; }

	[Required, MinLength(1), MaxLength(500)]
	public List<Guid> EnrollmentIds { get; set; } = [];
	public string? PreviewToken { get; set; }
}

public class HouseAssignmentPreviewDto
{
	public string PreviewToken { get; set; } = "";
	public List<HouseManagementStudentDto> Students { get; set; } = [];
	public List<HouseManagementStudentDto> ClearedHouseLeaders { get; set; } = [];
	public Guid? TargetHouseId { get; set; }
	public string? TargetHouseName { get; set; }
}

public class ReplaceStudentLeaderRequest
{
	public Guid AcademicMediumId { get; set; }
	public Guid? EnrollmentId { get; set; }

	[Required]
	public string ConcurrencyStamp { get; set; } = "";
}

public class HouseManagementHousePageDto : PagedResultDto<HouseManagementHouseDto>
{
	public bool CanManage { get; set; }
}
