using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ISalarySlipAppService _salarySlipAppService;

	public SalarySlipAppService_Tests()
	{
		_salarySlipAppService = GetRequiredService<ISalarySlipAppService>();
	}

	[Fact]
	public async Task GetSalarySlipAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GetSalarySlipAsync(Guid.NewGuid(), "2026-04"));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public async Task GenerateSalarySlipAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var input = new GenerateSalarySlipInput
		{
			EmployeeId = Guid.NewGuid(),
			MonthYear = "2026-04",
			ForceRegenerate = false
		};

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GenerateSalarySlipAsync(input));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	[Fact]
	public async Task GenerateSalarySlipPdfAsync_Should_Deny_When_Employee_Stage_Is_Unresolved()
	{
		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _salarySlipAppService.GenerateSalarySlipPdfAsync(Guid.NewGuid(), "2026-04"));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}
