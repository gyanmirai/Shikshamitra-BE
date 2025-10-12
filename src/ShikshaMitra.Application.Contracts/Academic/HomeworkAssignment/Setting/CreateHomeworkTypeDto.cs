using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public class CreateHomeworkTypeDto
{
	[Required]
	[StringLength(128)]
	public string Name { get; set; }

	[Required]
	public Guid EducationMediumId { get; set; }
}
