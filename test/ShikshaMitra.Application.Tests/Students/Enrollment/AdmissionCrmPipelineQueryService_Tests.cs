using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmPipelineQueryService_Tests :
	ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly AdmissionCrmPipelineQueryService _queryService;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly ICurrentTenant _currentTenant;

	public AdmissionCrmPipelineQueryService_Tests()
	{
		_queryService = GetRequiredService<AdmissionCrmPipelineQueryService>();
		_leadRepository = GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		_timelineRepository = GetRequiredService<IRepository<AdmissionCrmTimelineEvent, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Fixed_columns_translate_and_filter_in_the_database()
	{
		var tenantId = Guid.NewGuid();
		var academicYearId = Guid.NewGuid();

		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var newInquiry = CreateLead(tenantId, academicYearId, "New");
				var followUp = CreateLead(tenantId, academicYearId, "Follow");
				followUp.ChangeStage(AdmissionInquiryStage.FollowUp);
				await _leadRepository.InsertManyAsync([newInquiry, followUp], autoSave: true);

				var source = (await _leadRepository.GetQueryableAsync())
					.Where(lead => lead.AcademicYearId == academicYearId);
				var newInquiryQuery = await _queryService.ApplyColumnFilterAsync(
					source,
					AdmissionCrmPipelineColumnKey.NewInquiry,
					tenantId);
				var followUpQuery = await _queryService.ApplyColumnFilterAsync(
					source,
					AdmissionCrmPipelineColumnKey.FollowUp,
					tenantId);

				var newInquiryIds = await newInquiryQuery.Select(lead => lead.Id).ToListAsync();
				var followUpIds = await followUpQuery.Select(lead => lead.Id).ToListAsync();

				newInquiryIds.ShouldBe([newInquiry.Id]);
				followUpIds.ShouldBe([followUp.Id]);
			});
		}
	}

	[Fact]
	public async Task Timeline_utc_sort_column_translates_in_the_database()
	{
		var tenantId = Guid.NewGuid();
		var academicYearId = Guid.NewGuid();
		var leadId = Guid.NewGuid();
		var earlier = DateTimeOffset.UtcNow.AddMinutes(-5);
		var later = DateTimeOffset.UtcNow;

		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await _timelineRepository.InsertManyAsync(
					[
						new AdmissionCrmTimelineEvent(
							Guid.NewGuid(),
							tenantId,
							leadId,
							academicYearId,
							AdmissionCrmTimelineEventType.LeadCreated,
							"Earlier",
							null,
							null,
							null,
							earlier),
						new AdmissionCrmTimelineEvent(
							Guid.NewGuid(),
							tenantId,
							leadId,
							academicYearId,
							AdmissionCrmTimelineEventType.NoteAdded,
							"Later",
							null,
							null,
							null,
							later)
					],
					autoSave: true);

				var events = await (await _timelineRepository.GetQueryableAsync())
					.Where(item => item.LeadId == leadId)
					.OrderByDescending(item => item.OccurredAtUtc)
					.ThenByDescending(item => item.Id)
					.Skip(0)
					.Take(10)
					.ToListAsync();

				events.Select(item => item.Title).ShouldBe(["Later", "Earlier"]);
			});
		}
	}

	[Fact]
	public async Task Report_rows_group_fixed_columns_in_the_database()
	{
		var tenantId = Guid.NewGuid();
		var academicYearId = Guid.NewGuid();
		var standardId = Guid.NewGuid();

		using (_currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var firstInquiry = CreateLead(
					tenantId,
					academicYearId,
					"First",
					"Walk-in",
					standardId);
				var secondInquiry = CreateLead(
					tenantId,
					academicYearId,
					"Second",
					"Walk-in",
					standardId);
				var followUp = CreateLead(
					tenantId,
					academicYearId,
					"Follow",
					null,
					null);
				followUp.ChangeStage(AdmissionInquiryStage.FollowUp);
				await _leadRepository.InsertManyAsync(
					[firstInquiry, secondInquiry, followUp],
					autoSave: true);

				var source = (await _leadRepository.GetQueryableAsync())
					.Where(lead => lead.AcademicYearId == academicYearId);
				var rows = await _queryService.BuildReportRowsAsync(source, tenantId);
				var counts = await rows
					.Where(row => row.Column.HasValue)
					.GroupBy(row => row.Column)
					.Select(group => new
					{
						Column = group.Key,
						Count = group.Count()
					})
					.ToListAsync();

				counts.Single(row => row.Column == AdmissionCrmPipelineColumnKey.NewInquiry)
					.Count.ShouldBe(2);
				counts.Single(row => row.Column == AdmissionCrmPipelineColumnKey.FollowUp)
					.Count.ShouldBe(1);

				var sourceCounts = await rows
					.GroupBy(row =>
						row.InquirySourceName == null || row.InquirySourceName == string.Empty
							? "Unspecified"
							: row.InquirySourceName)
					.Select(group => new
					{
						SourceName = group.Key,
						InquiryCount = group.Count(),
						ExamScheduledCount = group.Count(row => row.ExamScheduled),
						ActivatedCount = group.Count(row =>
							row.Column == AdmissionCrmPipelineColumnKey.Activated)
					})
					.ToListAsync();
				sourceCounts.Single(row => row.SourceName == "Walk-in")
					.InquiryCount.ShouldBe(2);
				sourceCounts.Single(row => row.SourceName == "Unspecified")
					.InquiryCount.ShouldBe(1);

				var standardCounts = await rows
					.GroupBy(row => row.TargetStandardId)
					.Select(group => new
					{
						TargetStandardId = group.Key,
						InquiryCount = group.Count(),
						EligibleCount = group.Count(row => row.Eligible),
						RegistrationVerifiedCount = group.Count(row => row.RegistrationVerified)
					})
					.ToListAsync();
				standardCounts.Single(row => row.TargetStandardId == standardId)
					.InquiryCount.ShouldBe(2);
				standardCounts.Single(row => row.TargetStandardId == null)
					.InquiryCount.ShouldBe(1);
			});
		}
	}

	private static AdmissionInquiryLead CreateLead(
		Guid tenantId,
		Guid academicYearId,
		string name,
		string? inquirySource = null,
		Guid? targetStandardId = null)
	{
		return new AdmissionInquiryLead(
			Guid.NewGuid(),
			tenantId,
			academicYearId,
			null,
			inquirySource,
			AdmissionInquiryChannel.WalkInReception,
			null,
			null,
			"en",
			name,
			null,
			"Applicant",
			null,
			"Parent",
			$"9{Random.Shared.NextInt64(100000000, 999999999)}",
			null,
			"Fresh admission",
			targetStandardId,
			"Grade 1",
			null,
			DateTimeOffset.UtcNow);
	}
}
