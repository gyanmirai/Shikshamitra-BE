using ShikshaMitra.Library.Fines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Members;

public class CreateMemberDto
{
	public string Name { get; set; }
	public MemberRole Role { get; set; }
}
