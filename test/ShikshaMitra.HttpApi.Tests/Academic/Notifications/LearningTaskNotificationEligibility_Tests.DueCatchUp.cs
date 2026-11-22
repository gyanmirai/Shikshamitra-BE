using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private async Task AssertLateAdmissionCatchUpAsync(string eventKey, Guid sourceId, Guid homeworkId, Guid divisionId,
		DateTimeOffset scanAt, bool settleBeforeCancellation = false)
	{
		var original = (await ReadReminderIntentsAsync()).ShouldHaveSingleItem();
		var operationId = original.CampaignId!.Value;
		using var scope = Factory.Services.CreateScope();
		var workers = scope.ServiceProvider;
		using var school = workers.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		var outcomes = workers.GetRequiredService<NotificationOutcomeProjector>();

		await SettleWithoutDeliveryAsync(original.Id, scanAt.AddMinutes(3));
		(await ReadReminderOperationAsync()).Status.ShouldBe(CommunicationCampaignStatus.Cancelled);
		var lateStudentId = await EnrollStudentAsync();
		await ScanDueSourcesAsync(workers, scanAt.AddMinutes(4));
		var captured = await ReadReminderIntentsAsync();
		captured.Length.ShouldBe(2);
		var catchUp = captured.Single(x => x.Id != original.Id);
		catchUp.CampaignId.ShouldBe(operationId);
		catchUp.ExpiresAt.ShouldBe(original.ExpiresAt);
		catchUp.Status.ShouldBe(NotificationIntentState.Ready);
		captured.Single(x => x.Id == original.Id).Status.ShouldBe(NotificationIntentState.Completed);
		var roster = JsonSerializer.Deserialize<NotificationRecipientRefDto[]>(catchUp.AffectedRecipientsJson, JsonOptions);
		roster!.ShouldHaveSingleItem().StudentId.ShouldBe(lateStudentId);
		(await ReadReminderOperationAsync()).Status.ShouldBe(CommunicationCampaignStatus.Queued);
		await ScanDueSourcesAsync(workers, scanAt.AddMinutes(5));
		(await ReadReminderIntentsAsync()).Length.ShouldBe(2);

		if (settleBeforeCancellation) await SettleWithoutDeliveryAsync(catchUp.Id, scanAt.AddMinutes(6));
		using var cancelled = await Client.PostAsync($"/api/academic/notifications/operations/{operationId}/cancel", null);
		cancelled.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
		var stopped = await ReadReminderOperationAsync();
		stopped.RejectionReason.ShouldBe(NotificationErrorCodes.CancelledBeforeDelivery);
		stopped.Status.ShouldBe(CommunicationCampaignStatus.Cancelled);
		await EnrollStudentAsync();
		await ScanDueSourcesAsync(workers, scanAt.AddMinutes(7));
		(await ReadReminderIntentsAsync()).Length.ShouldBe(2);

		async Task<NotificationIntent[]> ReadReminderIntentsAsync()
		{
			NotificationIntent[] rows = [];
			await InSchoolAsync(async services =>
			{
				var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
				rows = (await repository.GetListAsync(x => x.TenantId == TenantId && x.EventKey == eventKey && x.SourceId == sourceId)).ToArray();
			});
			return rows;
		}

		async Task<CommunicationCampaign> ReadReminderOperationAsync()
		{
			CommunicationCampaign campaign = null!;
			await InSchoolAsync(async services =>
			{
				campaign = await services.GetRequiredService<IRepository<CommunicationCampaign, Guid>>().GetAsync(operationId);
			});
			return campaign;
		}

		async Task SettleWithoutDeliveryAsync(Guid intentId, DateTimeOffset now)
		{
			await InSchoolAsync(async services =>
			{
				var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
				var row = await repository.GetAsync(intentId);
				var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var lease = Guid.NewGuid();
				var tracked = db.Entry(row);
				tracked.Property(x => x.Status).CurrentValue = NotificationIntentState.Processing;
				tracked.Property(x => x.LeaseId).CurrentValue = lease;
				tracked.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(2);
				row.CompletePlanning(lease, now);
				row.CompleteDelivery(NotificationIntentState.Completed, "NoEligibleRecipients");
				await repository.UpdateAsync(row);
			});
			await outcomes.RefreshBatchAsync(100, CancellationToken.None);
		}

		async Task<Guid> EnrollStudentAsync()
		{
			var studentId = Guid.NewGuid();
			await InSchoolAsync(async services =>
			{
				var homework = await services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().GetAsync(homeworkId);
				var subject = await services.GetRequiredService<IRepository<AssignedSubject, Guid>>()
					.GetAsync(x => x.SubjectId == homework.SubjectId && x.StandardId == homework.StandardId);
				var year = Factory.SeededAcademicYearId;
				var medium = Factory.SeededMediumId;
				await InsertAsync(services, new Student(studentId, TenantId, "Late", null, "Learner", year, Factory.SeededBoardId,
					medium, Factory.SeededMediumStageId, homework.StandardId, divisionId, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), TenantId, studentId, year, homework.StandardId, divisionId));
				var elective = new StudentElectiveSelection(Guid.NewGuid(), TenantId, studentId, subject.ElectiveGroupId!.Value, homework.SubjectId, medium);
				elective.LockSelection();
				await InsertAsync(services, elective);
			});
			return studentId;
		}
	}
}
