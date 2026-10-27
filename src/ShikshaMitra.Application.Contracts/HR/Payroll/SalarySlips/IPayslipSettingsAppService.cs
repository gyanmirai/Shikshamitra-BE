using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public interface IPayslipSettingsAppService : IApplicationService
{
	Task<string> UploadLogoAsync(string contentType, byte[] bytes);
	Task<PayslipSettingsDto> GetAsync();
	Task<byte[]> PreviewAsync(SalaryDocumentPresentation input);
	Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input);
}
