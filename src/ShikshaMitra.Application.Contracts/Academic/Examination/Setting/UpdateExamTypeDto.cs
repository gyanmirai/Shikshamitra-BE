using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public class UpdateExamTypeDto
{
	public required string Name { get; set; }
	public string? Description { get; set; }
}
