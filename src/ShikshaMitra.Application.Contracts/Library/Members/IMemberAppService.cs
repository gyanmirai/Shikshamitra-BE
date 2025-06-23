using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Members;

public interface IMemberAppService
{
	Task<List<MemberOverviewDto>> GetOverviewAsync();
	Task<MemberDto> GetAsync(Guid id);
    Task<MemberDto> CreateAsync(CreateMemberDto input);
}
