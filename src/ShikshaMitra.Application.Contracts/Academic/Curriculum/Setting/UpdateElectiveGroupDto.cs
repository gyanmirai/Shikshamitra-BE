using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum.Setting;
public class UpdateElectiveGroupDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public List<Guid> SubjectIds { get; set; } = new List<Guid>();
}
