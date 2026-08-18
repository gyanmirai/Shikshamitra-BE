using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOfferingContract_Tests
{
	[Fact]
	public void Bulk_contract_rejects_duplicate_standards()
	{
		var standardId = Guid.NewGuid();
		var input = ValidBulkInput();
		input.Items.Add(new BulkAdmissionOfferingItemDto { StandardId = standardId, Code = "GRADE-1" });
		input.Items.Add(new BulkAdmissionOfferingItemDto { StandardId = standardId, Code = "GRADE-1-COPY" });

		var result = new BulkCreateAdmissionOfferingsDtoValidator().Validate(input);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.ErrorMessage.Contains("only once"));
	}

	[Fact]
	public void Override_window_requires_dates_in_order()
	{
		var input = ValidBulkInput();
		input.UseCycleDates = false;
		input.OpenDate = new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero);
		input.CloseDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

		new BulkCreateAdmissionOfferingsDtoValidator().Validate(input).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(nameof(AdmissionOfferingAppService.CreateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Create)]
	[InlineData(nameof(AdmissionOfferingAppService.UpdateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Edit)]
	[InlineData(nameof(AdmissionOfferingAppService.ActivateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Activate)]
	[InlineData(nameof(AdmissionOfferingAppService.DeleteAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete)]
	[InlineData(nameof(AdmissionOfferingAppService.BulkCreateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.BulkCreate)]
	public void Command_endpoints_require_granular_permission(string methodName, string permission)
	{
		var method = typeof(AdmissionOfferingAppService).GetMethods().Single(value => value.Name == methodName);

		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == permission);
	}

	private static BulkCreateAdmissionOfferingsDto ValidBulkInput() => new()
	{
		BoardId = Guid.NewGuid(),
		MediumId = Guid.NewGuid(),
		StageId = Guid.NewGuid(),
		UseCycleDates = true,
		WorkflowDefinitionId = Guid.NewGuid(),
		InquiryFormDefinitionId = Guid.NewGuid(),
		ApplicationFormDefinitionId = Guid.NewGuid()
	};
}
