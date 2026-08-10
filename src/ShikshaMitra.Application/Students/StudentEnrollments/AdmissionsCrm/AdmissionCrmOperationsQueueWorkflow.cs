using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsQueueWorkflow(
	AdmissionCrmOperationsDataReader dataReader,
	AdmissionCrmOperationsQueueAccess access) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId)
	{
		var data = await dataReader.LoadAsync(academicYearId, nameof(GetOverviewAsync));
		var permittedQueueTypes = await access.GetPermittedQueueTypesAsync();
		var queueItems = permittedQueueTypes.ToDictionary(x => x, x => AdmissionCrmOperationsQueueBuilder.BuildQueueItems(data, x));
		var queueCounts = queueItems.Select(x => new AdmissionCrmOperationsQueueCountDto
		{
			QueueType = x.Key,
			Label = AdmissionCrmOperationsQueueBuilder.GetQueueLabel(x.Key),
			Count = x.Value.Count,
			UrgentCount = x.Value.Count(item => item.IsUrgent)
		}).ToList();
		var urgentItems = queueItems
			.SelectMany(x => x.Value.Where(item => item.IsUrgent).Select(item => new AdmissionCrmEnrollmentOverviewItemDto
			{
				Workspace = AdmissionCrmOperationsQueueBuilder.GetQueueWorkspace(x.Key),
				QueueType = x.Key,
				Item = item
			}))
			.OrderBy(x => x.Item.DueAt ?? DateTimeOffset.MaxValue)
			.ThenByDescending(x => x.Item.AgeDays)
			.Take(12)
			.ToList();
		var now = Clock.Now;
		var canViewOffers = permittedQueueTypes.Any(AdmissionCrmOperationsQueueBuilder.IsOfferQueue);
		var expiringOfferCount = canViewOffers
			? data.Offers.Count(x =>
				x.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened &&
				x.ValidUntil <= now.AddDays(3))
			: 0;

		return new AdmissionCrmEnrollmentOverviewDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = now,
			UnassignedLeadCount = data.Leads.Count(x => !x.OwnerUserId.HasValue),
			UpcomingExamCount = permittedQueueTypes.Any(AdmissionCrmOperationsQueueBuilder.IsExamQueue)
				? data.Exams.Count(x => x.ExamStartTime.UtcDateTime >= now &&
					x.ExamStartTime.UtcDateTime <= now.AddDays(7) &&
					x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked)
				: 0,
			ExamNoShowCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.NoShow),
			PendingPaperEntryCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PendingPaperEntry),
			ExpiringRegistrationCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ExpiringRegistrationLink),
			DocumentFollowUpCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.RejectedDocument) +
				AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.DocumentVerificationBacklog),
			FeeFollowUpCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PaymentCondition),
			PendingApprovalCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.FinalApproval) +
				AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.PolicyException) +
				AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.RouteAppealReview),
			ExpiringOfferCount = expiringOfferCount,
			ClassPlacementRiskCount = canViewOffers ? data.SeatPools.Count(x => x.IsOverCapacity) : 0,
			ActivationFailureCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ActivationRepair),
			HandoffBlockerCount = AdmissionCrmOperationsQueueBuilder.GetQueueCount(queueItems, AdmissionCrmOperationsQueueType.ServiceHandoffException),
			UrgentCount = queueCounts.Sum(x => x.UrgentCount),
			Queues = queueCounts,
			UrgentItems = urgentItems
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId)
	{
		var data = await dataReader.LoadAsync(academicYearId, nameof(GetQueueSummaryAsync));
		var permittedQueueTypes = await access.GetPermittedQueueTypesAsync();
		var queues = permittedQueueTypes
			.Select(queueType =>
			{
				var items = AdmissionCrmOperationsQueueBuilder.BuildQueueItems(data, queueType);
				return new AdmissionCrmOperationsQueueCountDto
				{
					QueueType = queueType,
					Label = AdmissionCrmOperationsQueueBuilder.GetQueueLabel(queueType),
					Count = items.Count,
					UrgentCount = items.Count(x => x.IsUrgent)
				};
			})
			.ToList();

		return new AdmissionCrmOperationsQueueSummaryDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = Clock.Now,
			Queues = queues
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(
		Guid academicYearId,
		AdmissionCrmOperationsQueueType queueType,
		GetAdmissionCrmOperationsQueueInput input)
	{
		if (!Enum.IsDefined(queueType))
		{
			throw new BusinessException("AdmissionCrm:InvalidOperationsQueueType");
		}
		var permittedQueueTypes = await access.GetPermittedQueueTypesAsync();
		if (!permittedQueueTypes.Contains(queueType))
		{
			throw new AbpAuthorizationException("The current user cannot access this admissions queue.");
		}

		var data = await dataReader.LoadAsync(academicYearId, nameof(GetQueueAsync));
		var items = AdmissionCrmOperationsQueueBuilder.BuildQueueItems(data, queueType).AsEnumerable();
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			var applicantNumber = AdmissionCrmOperationsFormat.ParseApplicantNumber(input.Search);
			items = items.Where(x => (applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				x.ApplicantName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				(x.Details?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var applicantSearch = input.ApplicantSearch.Trim();
			var applicantNumber = AdmissionCrmOperationsFormat.ParseApplicantNumber(input.ApplicantSearch);
			items = items.Where(x =>
				(applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				x.ApplicantName.Contains(applicantSearch, StringComparison.OrdinalIgnoreCase));
		}
		if (!string.IsNullOrWhiteSpace(input.ActionSearch))
		{
			var actionSearch = input.ActionSearch.Trim();
			items = items.Where(x => x.Title.Contains(actionSearch, StringComparison.OrdinalIgnoreCase) ||
				(x.Details?.Contains(actionSearch, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (input.OwnerUserId.HasValue) items = items.Where(x => x.OwnerUserId == input.OwnerUserId);
		if (input.UrgentOnly == true) items = items.Where(x => x.IsUrgent);
		if (input.MinimumAgeDays.HasValue) items = items.Where(x => x.AgeDays >= input.MinimumAgeDays.Value);
		if (input.DueBefore.HasValue) items = items.Where(x => x.DueAt.HasValue && x.DueAt <= input.DueBefore.Value);
		if (input.RegistrationStatus.HasValue) items = items.Where(x => x.Registration?.Status == input.RegistrationStatus);
		if (input.DocumentStatus.HasValue) items = items.Where(x => x.Registration?.CurrentDocumentStatus == input.DocumentStatus);
		if (input.OfferStatus.HasValue) items = items.Where(x => x.Offer?.OfferStatus == input.OfferStatus);
		if (input.RouteType.HasValue) items = items.Where(x => x.Offer?.RouteType == input.RouteType);
		if (input.SeatPoolId.HasValue) items = items.Where(x => x.Offer?.SeatPoolId == input.SeatPoolId);
		if (input.ActivationStatus.HasValue) items = items.Where(x => x.Activation?.Status == input.ActivationStatus);
		if (input.ServiceHandoffStatus.HasValue) items = items.Where(x => x.Operation?.ServiceHandoffStatus == input.ServiceHandoffStatus);
		if (input.RegistrationLinkExpiresFrom.HasValue)
			items = items.Where(x => x.Registration?.LinkExpiresAt >= input.RegistrationLinkExpiresFrom.Value);
		if (input.RegistrationLinkExpiresUntil.HasValue)
			items = items.Where(x => x.Registration?.LinkExpiresAt < input.RegistrationLinkExpiresUntil.Value.AddDays(1));
		if (input.OfferExpiresFrom.HasValue)
			items = items.Where(x => x.Offer?.OfferExpiresAt >= input.OfferExpiresFrom.Value);
		if (input.OfferExpiresUntil.HasValue)
			items = items.Where(x => x.Offer?.OfferExpiresAt < input.OfferExpiresUntil.Value.AddDays(1));
		if (input.PaymentGateSatisfied.HasValue)
			items = items.Where(x => x.Offer?.PaymentGateSatisfied == input.PaymentGateSatisfied.Value);
		if (input.PlacementConfigured.HasValue)
			items = items.Where(x => x.Activation?.PlacementConfigured == input.PlacementConfigured.Value);
		if (input.AccountProvisioningPending.HasValue)
		{
			items = items.Where(x => x.Activation != null &&
				(x.Activation.RequireStudentAccount || x.Activation.RequireGuardianAccount) &&
				(input.AccountProvisioningPending.Value ? !x.Activation.StudentId.HasValue : x.Activation.StudentId.HasValue));
		}
		if (!string.IsNullOrWhiteSpace(input.RepairSearch))
		{
			var repairSearch = input.RepairSearch.Trim();
			items = items.Where(x =>
				(x.Activation?.RepairStep?.Contains(repairSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(x.Activation?.RepairReason?.Contains(repairSearch, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (input.AssignedToUserId.HasValue)
		{
			var delegatedOwners = await access.GetDelegatedOwnersAsync(academicYearId, queueType, input.AssignedToUserId.Value);
			items = items.Where(x => x.OwnerUserId == input.AssignedToUserId || (x.OwnerUserId.HasValue && delegatedOwners.Contains(x.OwnerUserId.Value)));
		}
		var filtered = AdmissionCrmOperationsFormat.ApplyQueueSorting(items, input.Sorting).ToList();
		return new AdmissionCrmOperationsQueueDto
		{
			AcademicYearId = academicYearId,
			QueueType = queueType,
			Label = AdmissionCrmOperationsQueueBuilder.GetQueueLabel(queueType),
			GeneratedAt = Clock.Now,
			TotalCount = filtered.Count,
			Items = filtered.Skip(input.SkipCount).Take(Math.Min(input.MaxResultCount, 200)).ToList()
		};
	}
}