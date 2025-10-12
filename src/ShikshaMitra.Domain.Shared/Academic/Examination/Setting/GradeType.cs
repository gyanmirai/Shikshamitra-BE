using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public enum GradeType
{
	[Description("A+")] APlus,
	[Description("A")] A,
	[Description("B+")] BPlus,
	[Description("B")] B,
	[Description("C+")] CPlus,
	[Description("C")] C,
	[Description("D+")] DPlus,
	[Description("D")] D,
	[Description("E")] E,
	[Description("F")] F
}
