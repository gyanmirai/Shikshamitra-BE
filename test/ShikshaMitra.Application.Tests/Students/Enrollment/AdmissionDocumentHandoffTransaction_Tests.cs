using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Document_finalization_and_handoff_roll_back_together_on_a_later_transaction_failure() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			var evidence = await AddHandoffEvidenceAsync(fixture, requirement);
			var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
			var studentsBefore = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var applicantBefore = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(ready.ApplicantId));
			var evidenceRepository = GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>();
			await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
			{
				// The shared application test module disables automatic transactions. Exercise a real
				// SQLite transaction here so persistence rollback is tested rather than only change tracking.
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try
				{
					var finalized = await FinalizeAsync(ready, true, "document-handoff-rollback");
					finalized.StudentId.ShouldBe(applicantBefore.ReservedStudentId);
					(await evidenceRepository.GetAsync(evidence.Id)).StudentId.ShouldBe(finalized.StudentId);
					(await _onboardingCaseRepository.CountAsync(item => item.ApplicationId == ready.Id)).ShouldBe(1);
					throw new InvalidOperationException("Simulated failure after finalization persistence.");
				}
				finally { await transaction.RollbackAsync(); }
			}));
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentsBefore);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == ready.Id))).ShouldBe(0);
			var storedEvidence = await WithUnitOfWorkAsync(() => evidenceRepository.GetAsync(evidence.Id));
			storedEvidence.StudentId.ShouldBeNull();
			storedEvidence.StudentLinkedAt.ShouldBeNull();
			var storedApplication = await _applicationAppService.GetAsync(ready.Id);
			storedApplication.Status.ShouldBe(AdmissionApplicationStatus.OfferAccepted);
			storedApplication.StudentId.ShouldBeNull();
			var applicantAfter = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(ready.ApplicantId));
			applicantAfter.ReservedStudentId.ShouldBe(applicantBefore.ReservedStudentId);
			applicantAfter.ExistingStudentId.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(applicantBefore.ReservedStudentId)))
				.ShouldBeNull();
			var retry = await FinalizeAsync(storedApplication, true, "document-handoff-rollback");
			retry.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			retry.StudentId.ShouldBe(applicantBefore.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(ready.ApplicantId)))
				.ReservedStudentId.ShouldBe(applicantBefore.ReservedStudentId);
		});
}
