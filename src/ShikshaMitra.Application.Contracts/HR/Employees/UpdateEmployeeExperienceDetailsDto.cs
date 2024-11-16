using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees;

public class UpdateEmployeeExperienceDetailsDto
{
	public List<WorkExperienceDto> WorkExperiences { get; set; } = new List<WorkExperienceDto>();
}