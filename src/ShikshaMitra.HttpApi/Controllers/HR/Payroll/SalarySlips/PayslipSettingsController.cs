using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/payroll/payslip-settings")]
public class PayslipSettingsController(IPayslipSettingsAppService settings) : AbpControllerBase
{
	[HttpPost("logo"), Consumes("multipart/form-data"), RequestSizeLimit(2097152 + 65536)]
	public async Task<object> UploadLogoAsync(Microsoft.AspNetCore.Http.IFormFile file)
	{
		if (file == null || file.Length is <= 0 or > 2097152) throw new BusinessException("HR:Payslip:LogoInvalid");
		await using var stream = file.OpenReadStream();
		using var memory = new System.IO.MemoryStream();
		await stream.CopyToAsync(memory);
		return new { BlobName = await settings.UploadLogoAsync(file.ContentType, memory.ToArray()) };
	}
	[HttpGet] public Task<PayslipSettingsDto> GetAsync() => settings.GetAsync();
	[HttpPost("preview")]
	public async Task<IActionResult> PreviewAsync(SalaryDocumentPresentation input)
	{
		Response.Headers.CacheControl = "no-store";
		return File(await settings.PreviewAsync(input), "application/pdf", "synthetic-payslip-preview.pdf");
	}
	[HttpPut] public Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input) => settings.SaveAsync(input);
}
