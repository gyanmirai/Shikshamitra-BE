using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public interface IFinanceExternalOperatorAppService : IApplicationService
{
	Task<FinanceExternalWaiverContextDto> GetWaiverContextAsync(FinanceExternalSourceReference source,
		FinanceExternalWaiverAction action, int page = 1, int pageSize = 20);
	Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetPostingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page = 1, int pageSize = 20, string? search = null);
	Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetBindingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page = 1, int pageSize = 20, string? search = null);
	Task<FinanceExternalOperatorContextDto> GetBindingContextAsync(FinanceExternalSourceReference source);
	Task<FinanceExternalOperatorContextDto> GetApprovalContextAsync(FinanceExternalSourceReference source);
	Task<PagedResultDto<FinanceExternalOperatorHistoryDto>> GetHistoryAsync(FinanceExternalSourceReference source,
		int page = 1, int pageSize = 20);
}
