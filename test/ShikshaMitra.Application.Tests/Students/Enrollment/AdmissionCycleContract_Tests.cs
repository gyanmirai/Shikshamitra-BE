using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCycleContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionCycleAppService.GetListAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCycleAppService.GetAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCycleAppService.GetImpactAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCycleAppService.GetHistoryAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCycleAppService.GetCapacityAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCycleAppService.CreateAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.UpdateAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.PublishAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.ReopenAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.CloneAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.UpdateWindowAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.ScheduleAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.ValidateAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.UpdateOfferingCapacityAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCycleAppService.CloseAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	[InlineData(nameof(AdmissionCycleAppService.ArchiveAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	[InlineData(nameof(AdmissionCycleAppService.DeleteAsync), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public void Every_operation_uses_only_view_add_or_delete(string methodName, string permission)
	{
		var method = typeof(AdmissionCycleAppService).GetMethods().Single(value => value.Name == methodName);

		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == permission);
	}

	[Fact]
	public void Create_contract_rejects_invalid_dates_and_accepts_future_academic_year_windows()
	{
		var invalid = ValidCreateInput();
		invalid.EndDate = invalid.StartDate;
		new CreateUpdateAdmissionCycleDtoValidator().Validate(invalid).IsValid.ShouldBeFalse();

		var valid = ValidCreateInput();
		new CreateUpdateAdmissionCycleDtoValidator().Validate(valid).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Governed_actions_require_reason_and_concurrency_stamp()
	{
		new AdmissionCycleActionDtoValidator().Validate(new AdmissionCycleActionDto()).IsValid.ShouldBeFalse();
		new AdmissionCycleActionDtoValidator().Validate(new AdmissionCycleActionDto
		{
			ConcurrencyStamp = "stamp",
			Reason = "Admissions team approved closure."
		}).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Schedule_requires_at_least_one_action()
	{
		new ScheduleAdmissionCycleDtoValidator().Validate(new ScheduleAdmissionCycleDto
		{
			ConcurrencyStamp = "stamp"
		}).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Capacity_change_requires_a_valid_limit_and_reason()
	{
		var validator = new UpdateAdmissionCycleOfferingCapacityDtoValidator();
		validator.Validate(new UpdateAdmissionCycleOfferingCapacityDto()).IsValid.ShouldBeFalse();
		validator.Validate(new UpdateAdmissionCycleOfferingCapacityDto
		{
			Capacity = 40,
			Reason = "Approved intake for the admission period."
		}).IsValid.ShouldBeTrue();
		validator.Validate(new UpdateAdmissionCycleOfferingCapacityDto
		{
			Capacity = 100001,
			Reason = "Invalid upper limit."
		}).IsValid.ShouldBeFalse();
	}

	private static CreateUpdateAdmissionCycleDto ValidCreateInput() => new()
	{
		Name = "Fresh Admission 2027-28",
		Code = "ADM-2027",
		AcademicYearId = Guid.NewGuid(),
		StartDate = new DateTime(2026, 10, 1),
		EndDate = new DateTime(2027, 3, 31)
	};
}
