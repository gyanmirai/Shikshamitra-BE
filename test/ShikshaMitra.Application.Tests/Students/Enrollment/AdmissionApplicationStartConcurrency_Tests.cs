using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Application_start_should_preserve_a_concurrent_qualification_email_update() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var application = await WithUnitOfWorkAsync(async () =>
			{
				// The dispatcher uses its own UOW, as the background worker does after the
				// request has loaded the inquiry. No email transport runs for this fixture.
				var loaded = await repository.GetAsync(inquiry.Id);
				await GetRequiredService<AdmissionInquiryQualificationEmailDispatcher>().DispatchAsync(inquiry.Id);
				loaded.GetProperty<string>("QualificationEmailStatus").ShouldBeNull();
				return await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			});
			var saved = await WithUnitOfWorkAsync(() => repository.GetAsync(inquiry.Id));
			saved.Status.ShouldBe(AdmissionInquiryStatus.ApplicationStarted);
			saved.GetProperty<string>("QualificationEmailStatus").ShouldBe("Skipped");
			application.InquiryId.ShouldBe(inquiry.Id);
			(await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id)).Id.ShouldBe(application.Id);
			(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync(item => item.InquiryId == inquiry.Id))).ShouldBe(1);
		});

	[Theory]
	[InlineData(AdmissionInquiryStatus.New)]
	[InlineData(AdmissionInquiryStatus.NotInterested)]
	public Task Application_start_should_revalidate_interest_after_a_concurrent_status_change(AdmissionInquiryStatus status) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			await WithUnitOfWorkAsync(async () =>
			{
				await repository.GetAsync(inquiry.Id);
				using (var concurrent = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
				{
					var current = await repository.GetAsync(inquiry.Id);
					current.SetStatus(status);
					await repository.UpdateAsync(current, true);
					await concurrent.CompleteAsync();
				}
				(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(
					inquiry.ApplicantId!.Value, offering.Id, inquiry.Id)))
					.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.QualificationRequired);
			});
			(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync(item => item.InquiryId == inquiry.Id))).ShouldBe(0);
			(await WithUnitOfWorkAsync(() => repository.GetAsync(inquiry.Id))).Status.ShouldBe(status);
		});

	[Fact]
	public Task Application_start_and_inquiry_handoff_should_roll_back_together() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var applicationId = Guid.Empty;
			await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try
				{
					applicationId = (await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id)).Id;
					throw new InvalidOperationException("Simulated failure after application handoff persistence.");
				}
				finally { await transaction.RollbackAsync(); }
			}));
			applicationId.ShouldNotBe(Guid.Empty);
			(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync(item => item.InquiryId == inquiry.Id))).ShouldBe(0);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id)))
				.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormInstance, Guid>>()
				.CountAsync(item => item.ApplicationId == applicationId))).ShouldBe(0);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>()
				.CountAsync(item => item.ApplicationId == applicationId))).ShouldBe(0);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>()
				.CountAsync(item => item.ApplicationId == applicationId))).ShouldBe(0);
		});
}
