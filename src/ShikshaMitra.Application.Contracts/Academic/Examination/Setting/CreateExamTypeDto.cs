using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public class CreateExamTypeDto
{
	[Required]
	public string Name { get; set; }
	public string? Description { get; set; }
	public Guid EducationMediumId { get; set; }
}
