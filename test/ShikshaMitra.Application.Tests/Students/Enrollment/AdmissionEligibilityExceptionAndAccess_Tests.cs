using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{

	private static ClaimsPrincipal EligibilityPrincipal(Guid userId, string name, string role = "admin") => new(new ClaimsIdentity(
	[
		new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, name),
		new Claim(AbpClaimTypes.Role, role)
	], "Test"));
}
