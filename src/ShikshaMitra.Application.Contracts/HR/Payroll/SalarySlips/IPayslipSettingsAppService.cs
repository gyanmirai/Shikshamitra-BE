using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public interface IPayslipSettingsAppService : IApplicationService
{
	Task<PayslipSettingsDto> GetAsync();
	Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input);
}
