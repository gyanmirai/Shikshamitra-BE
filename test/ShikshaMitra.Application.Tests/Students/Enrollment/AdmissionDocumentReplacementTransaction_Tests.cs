using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Document_onboarding_replacement_preserves_previous_file_on_rollback_and_cleans_it_after_commit() =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var fixture = await CreateCertificationFixtureAsync("ReplacementRollback");
			var finalized = await FinalizeAsync(fixture.Application, true, "replacement-rollback");
			var service = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var workspace = await service.GetAsync(finalized.OnboardingCaseId);
			using (var first = new RemoteStreamContent(new MemoryStream(HandoffPdf), "first.pdf", "application/pdf", HandoffPdf.Length))
				workspace = await service.UploadAsync(workspace.CaseId, new()
				{
					DocumentTypeId = type.Id, ConcurrencyStamp = workspace.ConcurrencyStamp, File = first
				});
			var documents = GetRequiredService<IRepository<StudentDocument, Guid>>();
			var stored = await WithUnitOfWorkAsync(() => documents.GetAsync(item => item.StudentId == finalized.StudentId));
			var previousBlob = stored.BlobName;
			var blobs = GetRequiredService<IBlobContainer>();
			(await blobs.ExistsAsync(previousBlob)).ShouldBeTrue();
			var replacement = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\nDifferent replacement document\n%%EOF");
			await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try
				{
					using var file = new RemoteStreamContent(new MemoryStream(replacement), "replacement.pdf", "application/pdf", replacement.Length);
					await service.UploadAsync(workspace.CaseId, new()
					{
						DocumentTypeId = type.Id, ConcurrencyStamp = workspace.ConcurrencyStamp, File = file
					});
					(await documents.GetAsync(stored.Id)).BlobName.ShouldNotBe(previousBlob);
					(await blobs.ExistsAsync(previousBlob)).ShouldBeTrue();
					throw new InvalidOperationException("Simulated failure after upload and readiness persistence.");
				}
				finally { await transaction.RollbackAsync(); }
			}));
			(await WithUnitOfWorkAsync(() => documents.GetAsync(stored.Id))).BlobName.ShouldBe(previousBlob);
			(await blobs.ExistsAsync(previousBlob)).ShouldBeTrue();
			var retry = await service.GetAsync(workspace.CaseId);
			using var retryFile = new RemoteStreamContent(new MemoryStream(replacement), "replacement.pdf", "application/pdf", replacement.Length);
			await service.UploadAsync(retry.CaseId, new()
			{
				DocumentTypeId = type.Id, ConcurrencyStamp = retry.ConcurrencyStamp, File = retryFile
			});
			(await blobs.ExistsAsync(previousBlob)).ShouldBeFalse();
			var committed = await WithUnitOfWorkAsync(() => documents.GetAsync(stored.Id));
			(await blobs.ExistsAsync(committed.BlobName)).ShouldBeTrue();
		});
}
