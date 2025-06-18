using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.Members;

public class MemberDto : EntityDto<Guid>
{
	public string Name { get; set; }
	public string Role { get; set; }
}
