using ShikshaMitra.Library.Fines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Members;

public class MemberOverviewDto
{
	public string MemberId { get; set; }
	public MemberRole Role { get; set; }
	public string Name { get; set; }

	public int CurrentIssue { get; set; }
    public int PendingReturn { get; set; }
    public int OverdueReturn { get; set; }
}
