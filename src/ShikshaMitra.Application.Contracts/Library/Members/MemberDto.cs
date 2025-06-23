using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.Members;

public class MemberDto : EntityDto<Guid>
{
	public string Name { get; set; }
	public string Role { get; set; }
}
