using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Members;

public class MemberAppService(
		IRepository<Member, Guid> memberRepository,
		IGuidGenerator guidGenerator
) : ApplicationService, IMemberAppService
 {
    public async Task<List<MemberOverviewDto>> GetOverviewAsync()
    {
        var members = await memberRepository.GetListAsync();

        var result = members.Select(m => new MemberOverviewDto
        {
            MemberId = m.Id.ToString(), 
            Name = m.Name,
            Role = m.Role
        }).ToList();

        return result;
    }
	public async Task<MemberDto> GetAsync(Guid id)
	{
		var member = await memberRepository.GetAsync(id);
		return ObjectMapper.Map<Member, MemberDto>(member);
	}
	public async Task<MemberDto> CreateAsync(CreateMemberDto createMemberDto)
	{
		var member = new Member(
			guidGenerator.Create(),
			createMemberDto.Name,
			createMemberDto.Role,
			CurrentTenant.Id
		);

		await memberRepository.InsertAsync(member, autoSave: true);
		return ObjectMapper.Map<Member, MemberDto>(member);
	}
 }