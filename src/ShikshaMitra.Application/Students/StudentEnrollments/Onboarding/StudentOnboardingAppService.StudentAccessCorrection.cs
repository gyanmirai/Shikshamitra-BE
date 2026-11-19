using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialsWorkspaceDto> EnableStudentAppAccessAsync(Guid id, EnableStudentAppAccessDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure);
		EnsureCredentialsActor();
		var validation = await new EnableStudentAppAccessDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw PortalInvalid(validation.Errors[0].ErrorMessage);
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!await HasActiveStudentAccessEnrollmentAsync(item))
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (!item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) || snapshot is not string text)
			throw PortalInvalid("This case has no reviewed readiness snapshot. Review its requirements first.");
		JsonObject rules;
		try { rules = JsonNode.Parse(text) as JsonObject ?? throw new JsonException(); }
		catch (JsonException) { throw PortalInvalid("The saved case rules are unavailable for correction."); }
		if (StudentPortalAccountRules.IsEnabled(JsonSerializer.SerializeToElement(rules), "student"))
			return await MapCredentialsAsync(item);

		var previousAccess = rules["studentAccountAccess"]?.DeepClone();
		var previousRequired = rules["studentAccount"]?.DeepClone();
		// A case-specific access choice must never add a completion gate or automatic delivery.
		rules["studentAccountAccess"] = StudentPortalAccountRules.Optional;
		rules["studentAccount"] = false;
		item.ExtraProperties["ReadinessRulesSnapshot"] = rules.ToJsonString();
		await eventRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyAuditWriter>()
			.CreateOnboardingEvent(id, "StudentAppAccessEnabled", $"Optional Student app access enabled for this case. Reason: {input.Reason.Trim()}",
				JsonSerializer.Serialize(new { Previous = new { StudentAccountAccess = previousAccess, StudentAccount = previousRequired },
					Current = new { StudentAccountAccess = StudentPortalAccountRules.Optional, StudentAccount = false } })), false);
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCredentialsAsync(item);
	}

	private async Task<bool> HasActiveStudentAccessEnrollmentAsync(StudentOnboardingCase item)
	{
		if (!CurrentTenant.Id.HasValue || item.TenantId != CurrentTenant.Id || !item.StudentEnrollmentId.HasValue ||
			item.Status == StudentOnboardingStatus.Cancelled) return false;
		var enrollment = await enrollmentRepository.FindAsync(item.StudentEnrollmentId.Value);
		if (enrollment == null || !enrollment.IsActive || enrollment.TenantId != CurrentTenant.Id ||
			enrollment.StudentId != item.StudentId || enrollment.AcademicYearId != item.AcademicYearId) return false;
		var student = await studentRepository.FindAsync(item.StudentId);
		return student != null && student.TenantId == CurrentTenant.Id && student.Status == StudentLifeCycleStatusEnum.Enrolled;
	}
}
