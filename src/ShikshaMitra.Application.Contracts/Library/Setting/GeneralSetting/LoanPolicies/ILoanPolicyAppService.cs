using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;

public interface ILoanPolicyAppService : IApplicationService
{
	Task<LoanPolicyDto> GetAsync(Guid id);
	Task<PagedResultDto<LoanPolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input);
	Task<LoanPolicyDto> CreateAsync(CreateUpdateLoanPolicyDto input);
	Task<LoanPolicyDto> UpdateAsync(Guid id, CreateUpdateLoanPolicyDto input);
	Task DeleteAsync(Guid id);
}
