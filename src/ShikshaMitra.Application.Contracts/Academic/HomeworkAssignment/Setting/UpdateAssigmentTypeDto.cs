using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public class UpdateAssigmentTypeDto
{
	[Required]
	[StringLength(128)]
	public string Name { get; set; }
}
