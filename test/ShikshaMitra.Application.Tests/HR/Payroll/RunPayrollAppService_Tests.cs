using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class RunPayrollAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IRunPayrollAppService _runPayrollAppService;

	public RunPayrollAppService_Tests()
	{
		_runPayrollAppService = GetRequiredService<IRunPayrollAppService>();
	}

	[Fact]
	public async Task LockPayrollAsync_Should_Deny_When_UserScope_Has_No_MediumStages()
	{
		var request = new RunPayrollRequest
		{
			Month = "2026-04",
			EmployeeIds = new List<Guid> { Guid.NewGuid() }
		};

		var ex = await Should.ThrowAsync<BusinessException>(async () =>
			await _runPayrollAppService.LockPayrollAsync(request));

		ex.Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}
