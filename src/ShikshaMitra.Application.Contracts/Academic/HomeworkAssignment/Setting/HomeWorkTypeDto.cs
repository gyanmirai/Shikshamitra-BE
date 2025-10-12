using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public class HomeWorkTypeDto : EntityDto<Guid>
{
	public string Name { get; set; }
}
