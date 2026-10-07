using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public interface IPayslipDeliveryAppService : IApplicationService
{
	Task<List<PayslipDeliveryDto>> GetAsync(Guid salarySlipId);
	Task<PayslipDeliveryDto> SendAsync(SendPayslipDto input);
}
