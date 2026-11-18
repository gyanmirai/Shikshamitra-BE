using System;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Admission_return_provenance_validation_does_not_construct_return_operations(bool formerStudent) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReturnProvenance", linkExistingStudent: formerStudent, completeWorkflow: false);
			using var scope = ServiceProvider.GetRequiredService<ILifetimeScope>().BeginLifetimeScope(builder =>
			{
				foreach (var type in new[] { typeof(StudentRecordsAccessService), typeof(Students.StudentRecords.StudentRecordsPolicyResolver),
					typeof(StudentRecordTransitionAuthorityService), typeof(AdmissionOfferingRuntimeService), typeof(StudentRecordTransitionReadinessService),
					typeof(IRepository<StudentRecordTransition, Guid>), typeof(IRepository<StudentRecordTransitionBatch, Guid>),
					typeof(IRepository<StudentRecordTransitionEvent, Guid>), typeof(IRepository<StudentRecordLegacyOverride, Guid>),
					typeof(IRepository<StudentGuardianAccessReview, Guid>), typeof(IRepository<AdmissionFormSectionDefinition, Guid>),
					typeof(IRepository<TenantSettings.AcademicYears.AcademicYear, Guid>) })
					builder.Register<object>(_ => throw new InvalidOperationException($"Unrelated return operation constructed: {type.Name}"))
						.As(type);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				if (formerStudent)
				{
					var student = await _studentRepository.GetAsync(applicant.ExistingStudentId!.Value);
					student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
					await _studentRepository.UpdateAsync(student, true);
				}
				var returns = scope.Resolve<StudentRecordAdmissionReturnService>();
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				if (formerStudent)
					(await Should.ThrowAsync<BusinessException>(() => returns.ValidateFinalizationAsync(application, applicant)))
						.Code.ShouldBe("Students:Records:InvalidReturnApplication");
				else (await returns.ValidateFinalizationAsync(application, applicant)).ShouldBeNull();
			});
		});

	[Fact]
	public Task New_admission_finalizes_without_constructing_record_return_readiness() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("NoReturnDependencies", completeWorkflow: false);
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			using var scope = ServiceProvider.GetRequiredService<ILifetimeScope>().BeginLifetimeScope(builder =>
				builder.Register<StudentRecordTransitionReadinessService>(_ => throw new InvalidOperationException(
					"Unrelated return creation/activation readiness was constructed.")));
			var result = await WithUnitOfWorkAsync(() => scope.Resolve<AdmissionApplicationDecisionService>()
				.ApproveAndFinalizeAsync(application.Id, CombinedFinalization(application, "no-return-dependencies")));
			result.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			(await _applicationAppService.GetWorkflowAsync(application.Id)).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			var saved = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentOnboardingCase, Guid>>()
				.GetAsync(result.OnboardingCaseId));
			saved.StudentId.ShouldBe(result.StudentId);
			saved.ApplicationId.ShouldBe(application.Id);
			saved.GuardianContactVerifiedAt.ShouldBeNull();
		});
}
