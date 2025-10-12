using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Examination.Setting;
public class ExamTypeDto : EntityDto<Guid>
{
	public required string Name { get; set; }
	public required string Description { get; set; }
}
