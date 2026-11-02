using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	private async Task<StudentOnboardingCase> SeedCaseAsync(IServiceProvider services, Guid? owner, Guid? completedDivision = null,
		DashboardAcademicPath? academicPath = null)
	{
		var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		var path = academicPath ?? new(Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId);
		var cycle = new AdmissionCycle(Guid.NewGuid(), tenant, "Dashboard setup", Guid.NewGuid().ToString("N"), path.YearId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10));
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
		var offering = new AdmissionOffering(Guid.NewGuid(), tenant, cycle.Id, null, path.BoardId, path.MediumId, path.StageId, path.StandardId, Guid.NewGuid().ToString("N"), true, null, null);
		await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
		var family = new AdmissionFamily(Guid.NewGuid(), tenant, Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family, true);
		var student = new Student(Guid.NewGuid(), tenant, "Dashboard", null, "Fixture", path.YearId, path.BoardId, path.MediumId, path.StageId, path.StandardId, completedDivision, status: completedDivision.HasValue ? StudentLifeCycleStatusEnum.Enrolled : StudentLifeCycleStatusEnum.New);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		var applicant = new AdmissionApplicant(Guid.NewGuid(), tenant, family.Id, "Dashboard", null, "Fixture", null, null, null, student.Id);
		await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), tenant, "Dashboard", Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		var form = new AdmissionFormDefinition(Guid.NewGuid(), tenant, "Dashboard", Guid.NewGuid().ToString("N"), AdmissionFormLayoutType.SinglePage);
		await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, true);
		var application = new AdmissionApplication(Guid.NewGuid(), tenant, Guid.NewGuid().ToString("N"), applicant.Id, null, cycle.Id, offering.Id, workflow.Id, form.Id);
		await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, true);
		var item = new StudentOnboardingCase(Guid.NewGuid(), tenant, application.Id, applicant.Id, student.Id, offering.Id, path.YearId, path.BoardId, path.MediumId, path.StageId, path.StandardId, Guid.NewGuid().ToString(), null, DateTime.UtcNow.AddDays(5));
		if (owner.HasValue) item.AssignOwner(owner.Value);
		await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(item, true);
		if (completedDivision.HasValue)
		{
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, student.Id, path.YearId, path.StandardId, completedDivision, onboardingCaseId: item.Id);
			await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
			item.CompleteHistoricalImport(enrollment.Id, completedDivision, null, DateTime.UtcNow);
			await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().UpdateAsync(item, true);
		}
		return item;
	}

	private static async Task<DashboardAcademicPath> CreateDashboardAcademicPathAsync(IServiceProvider services)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
			await services.GetRequiredService<AcademicYearManager>().CreateAsync("Dashboard " + suffix, new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
		var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
			await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE), true);
		var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
			await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Dashboard medium"), true);
		var stage = await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
			await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Dashboard stage"), true);
		var standard = await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
			await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Dashboard class", 5, null), true);
		return new(year.Id, board.Id, medium.Id, stage.Id, standard.Id);
	}

	private sealed record DashboardAcademicPath(Guid YearId, Guid BoardId, Guid MediumId, Guid StageId, Guid StandardId);
}
