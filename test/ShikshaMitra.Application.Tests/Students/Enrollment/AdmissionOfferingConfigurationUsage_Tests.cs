using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOfferingConfigurationUsage_Tests
{
	private readonly IRepository<AdmissionOffering, Guid> _offerings = Substitute.For<IRepository<AdmissionOffering, Guid>>();
	private readonly IAdmissionOfferingConfigurationLock _configurationLock = Substitute.For<IAdmissionOfferingConfigurationLock>();

	[Fact]
	public async Task Unallocated_inquiry_has_no_offering_to_mark()
	{
		await CreateService().RecordAsync((AdmissionOffering?)null);
		await _configurationLock.DidNotReceive().AcquireAndRefreshAsync(Arg.Any<AdmissionOffering>());
		await _offerings.DidNotReceive().UpdateAsync(Arg.Any<AdmissionOffering>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Two_stale_first_intakes_coalesce_usage_after_the_lock_refresh()
	{
		var first = CreateOffering();
		var second = CopyOffering(first);
		using var gate = new SemaphoreSlim(1);
		var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var arrivals = 0;
		var persistedUsage = false;
		_configurationLock.AcquireAndRefreshAsync(Arg.Any<AdmissionOffering>()).Returns(async call =>
		{
			if (Interlocked.Increment(ref arrivals) == 2) bothArrived.SetResult();
			await bothArrived.Task;
			await gate.WaitAsync();
			if (persistedUsage)
			{
				call.Arg<AdmissionOffering>().SetProperty("AdmissionConfigurationUsed", true);
				gate.Release();
			}
		});
		_offerings.UpdateAsync(Arg.Any<AdmissionOffering>(), false, Arg.Any<CancellationToken>()).Returns(call =>
		{
			var stored = call.Arg<AdmissionOffering>();
			persistedUsage = stored.GetProperty<bool>("AdmissionConfigurationUsed");
			gate.Release();
			return Task.FromResult(stored);
		});
		var usage = CreateService();
		await Task.WhenAll(usage.RecordAsync(first), usage.RecordAsync(second));
		first.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
		second.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
		first.WorkflowDefinitionId.ShouldBe(second.WorkflowDefinitionId);
		await _offerings.Received(1).UpdateAsync(Arg.Any<AdmissionOffering>(), false, Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData("workflow")]
	[InlineData("inquiry")]
	[InlineData("registration")]
	[InlineData("routing")]
	[InlineData("identity")]
	[InlineData("active")]
	[InlineData("dates")]
	public async Task Stale_captured_configuration_cannot_be_marked_or_committed(string changed)
	{
		var offering = CreateOffering();
		_configurationLock.AcquireAndRefreshAsync(offering).Returns(_ =>
		{
			if (changed is "workflow" or "inquiry" or "registration")
				offering.ConfigureReferences(changed == "workflow" ? Guid.NewGuid() : offering.WorkflowDefinitionId,
					changed == "inquiry" ? Guid.NewGuid() : offering.InquiryFormDefinitionId,
					changed == "registration" ? Guid.NewGuid() : offering.ApplicationFormDefinitionId);
			if (changed == "routing") offering.UpdateRouting(AdmissionTrack.Management, AdmissionEntryMode.DirectRegistration, false);
			if (changed == "identity") offering.UpdateIdentity(offering.CampusId, offering.BoardId, offering.MediumId,
				offering.StageId, Guid.NewGuid(), false);
			if (changed == "active") offering.Deactivate();
			if (changed == "dates") offering.UpdateOperationalDetails(offering.Code, false,
				new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));
			return Task.CompletedTask;
		});
		(await Should.ThrowAsync<BusinessException>(() => CreateService().RecordAsync(offering)))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingConcurrencyConflict);
		offering.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeFalse();
		await _offerings.DidNotReceive().UpdateAsync(Arg.Any<AdmissionOffering>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Refresh_preserves_a_newer_operational_name_before_marking_usage()
	{
		var offering = CreateOffering();
		_configurationLock.AcquireAndRefreshAsync(offering).Returns(_ =>
		{
			offering.UpdateOperationalDetails(offering.Code, true, null, null, "Newer operational name");
			offering.ConcurrencyStamp = "newer-stamp";
			return Task.CompletedTask;
		});
		await CreateService().RecordAsync(offering);
		offering.Name.ShouldBe("Newer operational name");
		offering.ConcurrencyStamp.ShouldBe("newer-stamp");
		offering.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
	}

	[Fact]
	public async Task Missing_previous_offering_does_not_block_recording_the_live_target()
	{
		var previousId = Guid.NewGuid();
		var target = CreateOffering();
		_offerings.GetAsync(previousId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns<Task<AdmissionOffering>>(_ => throw new EntityNotFoundException(typeof(AdmissionOffering), previousId));
		_offerings.FindAsync(previousId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns((AdmissionOffering?)null);
		await CreateService().RecordTransferAsync(previousId, target);
		target.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
	}

	[Fact]
	public async Task Transfer_records_source_and_target_in_stable_id_order()
	{
		var source = CopyOffering(CreateOffering(), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
		var target = CopyOffering(source, Guid.Parse("00000001-0000-0000-0000-000000000001"));
		_offerings.FindAsync(source.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(source);
		var acquired = new List<Guid>();
		_configurationLock.AcquireAndRefreshAsync(Arg.Any<AdmissionOffering>()).Returns(call =>
		{
			acquired.Add(call.Arg<AdmissionOffering>().Id);
			return Task.CompletedTask;
		});
		await CreateService().RecordTransferAsync(source.Id, target);
		acquired.ShouldBe(source.Id.CompareTo(target.Id) < 0 ? new[] { source.Id, target.Id } : new[] { target.Id, source.Id });
		source.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
		target.GetProperty<bool>("AdmissionConfigurationUsed").ShouldBeTrue();
	}

	private AdmissionOfferingConfigurationUsageService CreateService() => new(_offerings,
		Substitute.For<IRepository<AdmissionInquiry, Guid>>(), Substitute.For<IRepository<AdmissionApplication, Guid>>(),
		Substitute.For<IDataFilter>(), _configurationLock);

	private static AdmissionOffering CreateOffering()
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LOCK-TEST", true, null, null);
		offering.ConfigureReferences(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		return offering;
	}

	private static AdmissionOffering CopyOffering(AdmissionOffering source, Guid? id = null)
	{
		var copy = new AdmissionOffering(id ?? source.Id, source.TenantId, source.AdmissionCycleId, source.CampusId,
			source.BoardId, source.MediumId, source.StageId, source.StandardId, source.Code, true, null, null);
		copy.ConfigureReferences(source.WorkflowDefinitionId, source.InquiryFormDefinitionId, source.ApplicationFormDefinitionId);
		return copy;
	}
}
