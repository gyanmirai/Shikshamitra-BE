using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	private async Task<StudentOnboardingCase> SeedCaseAsync(IServiceProvider services, Guid owner, Guid? completedDivision = null)
	{
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		var cycle = new AdmissionCycle(Guid.NewGuid(), tenant, "Dashboard setup", Guid.NewGuid().ToString("N"), Factory.SeededAcademicYearId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10));
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
		var offering = new AdmissionOffering(Guid.NewGuid(), tenant, cycle.Id, null, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Guid.NewGuid().ToString("N"), true, null, null);
		await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
		var family = new AdmissionFamily(Guid.NewGuid(), tenant, Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family, true);
		var student = new Student(Guid.NewGuid(), tenant, "Dashboard", null, "Fixture", Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, completedDivision, status: completedDivision.HasValue ? StudentLifeCycleStatusEnum.Enrolled : StudentLifeCycleStatusEnum.New);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		var applicant = new AdmissionApplicant(Guid.NewGuid(), tenant, family.Id, "Dashboard", null, "Fixture", null, null, null, student.Id);
		await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), tenant, "Dashboard", Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		var form = new AdmissionFormDefinition(Guid.NewGuid(), tenant, "Dashboard", Guid.NewGuid().ToString("N"), AdmissionFormLayoutType.SinglePage);
		await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, true);
		var application = new AdmissionApplication(Guid.NewGuid(), tenant, Guid.NewGuid().ToString("N"), applicant.Id, null, cycle.Id, offering.Id, workflow.Id, form.Id);
		await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, true);
		var item = new StudentOnboardingCase(Guid.NewGuid(), tenant, application.Id, applicant.Id, student.Id, offering.Id, Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Guid.NewGuid().ToString(), null, DateTime.UtcNow.AddDays(5));
		item.AssignOwner(owner);
		await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(item, true);
		if (completedDivision.HasValue)
		{
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, student.Id, Factory.SeededAcademicYearId, Factory.SeededStandardId, completedDivision, onboardingCaseId: item.Id);
			await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
			item.CompleteHistoricalImport(enrollment.Id, completedDivision, null, DateTime.UtcNow);
			await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().UpdateAsync(item, true);
		}
		return item;
	}
}
