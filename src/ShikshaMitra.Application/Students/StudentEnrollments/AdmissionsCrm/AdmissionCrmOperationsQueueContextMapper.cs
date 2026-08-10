using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmOperationsQueueContextMapper
{
	internal static void PopulateQueueContexts(
		AdmissionCrmOperationsData data,
		AdmissionCrmOperationsQueueType queueType,
		IReadOnlyCollection<AdmissionCrmOperationsQueueItemDto> items)
	{
		var currentDocuments = AdmissionCrmOperationsDecisionQueueCalculator.GetCurrentDocuments(data.Documents).ToList();
		foreach (var item in items)
		{
			var registration = item.RegistrationApplicationId.HasValue
				? data.Registrations.FirstOrDefault(x => x.Id == item.RegistrationApplicationId.Value)
				: data.Registrations.Where(x => x.LeadId == item.LeadId)
					.OrderByDescending(x => x.IssuedAt).FirstOrDefault();
			var checklist = registration == null
				? null
				: data.Checklists.Where(x => x.RegistrationApplicationId == registration.Id)
					.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
			IReadOnlyList<AdmissionCrmApplicantDocument> documents = checklist == null
				? []
				: currentDocuments.Where(x => x.ChecklistAssignmentId == checklist.Id).ToList();
			var currentDocument = item.DocumentId.HasValue
				? documents.FirstOrDefault(x => x.Id == item.DocumentId.Value)
				: null;
			if (registration != null || AdmissionCrmOperationsQueueBuilder.IsRegistrationQueue(queueType))
			{
				var resolvedCount = documents.Count(x => x.Status is AdmissionCrmApplicantDocumentStatus.Verified
					or AdmissionCrmApplicantDocumentStatus.Waived
					or AdmissionCrmApplicantDocumentStatus.OriginalSeen);
				item.Registration = new AdmissionCrmRegistrationQueueContextDto
				{
					RegistrationApplicationId = registration?.Id,
					Status = registration?.Status,
					FormNumber = registration?.FormNumber,
					Provenance = registration?.Provenance,
					DataEntryState = registration?.DataEntryState,
					PhysicalFormNumber = registration?.PhysicalFormNumber,
					PhysicalFormReceivedDate = registration?.PhysicalFormReceivedDate,
					CompletionPercentage = registration?.CompletionPercentage ?? 0,
					LinkExpiresAt = registration?.LinkExpiresAt,
					FeeStatus = registration?.FeeStatus,
					DocumentTotalCount = documents.Count,
					DocumentResolvedCount = resolvedCount,
					DocumentPendingCount = Math.Max(0, documents.Count - resolvedCount),
					CurrentDocumentStatus = currentDocument?.Status,
					ReadinessConfirmed = checklist?.ReadinessConfirmedAt.HasValue == true
				};
			}

			var offer = data.Offers.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.Version).ThenByDescending(x => x.CreationTime).FirstOrDefault();
			var decision = data.Decisions.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.DecidedAt ?? x.CreationTime).FirstOrDefault();
			var route = data.Routes.Where(x => x.LeadId == item.LeadId && x.IsPrimary)
				.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
			var quotation = data.Quotations.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.Version).FirstOrDefault();
			var seatPoolId = offer?.SeatPoolId ?? decision?.SeatPoolId;
			var seatPool = seatPoolId.HasValue
				? data.SeatPools.FirstOrDefault(x => x.Id == seatPoolId.Value)
				: null;
			if (offer != null || decision != null || route != null || quotation != null || AdmissionCrmOperationsQueueBuilder.IsOfferQueue(queueType))
			{
				item.Offer = new AdmissionCrmOfferQueueContextDto
				{
					OfferId = offer?.Id,
					OfferNumber = offer?.OfferNumber,
					OfferStatus = offer?.Status,
					OfferExpiresAt = offer?.ValidUntil,
					SeatPoolId = seatPoolId,
					SeatPoolName = seatPool?.Name,
					RemainingSeats = seatPool?.RemainingCapacity,
					DecisionStatus = decision?.Status,
					RouteType = route?.RouteType,
					RouteStatus = route?.Status,
					FinanceState = quotation?.FinanceState,
					PaymentGateSatisfied = quotation == null ? null : quotation.IsGateSatisfiedAt(data.Now)
				};
			}

			var activation = data.Activations.Where(x => x.LeadId == item.LeadId)
				.OrderByDescending(x => x.RequestedAt).FirstOrDefault();
			if (activation != null || AdmissionCrmOperationsQueueBuilder.IsActivationQueue(queueType))
			{
				item.Activation = new AdmissionCrmActivationQueueContextDto
				{
					ActivationId = activation?.Id,
					StudentId = activation?.StudentId,
					ActivationReference = activation?.ActivationReference,
					Status = activation?.Status,
					PlacementConfigured = activation != null && activation.StandardId != Guid.Empty && activation.DivisionId != Guid.Empty,
					RollNumber = activation?.RollNumber,
					RequireStudentAccount = activation?.RequireStudentAccount ?? false,
					RequireGuardianAccount = activation?.RequireGuardianAccount ?? false,
					AttemptCount = activation?.AttemptCount ?? 0,
					RepairStep = activation?.RepairStep,
					RepairReason = activation?.RepairReason
				};
			}

			var handoff = item.ServiceHandoffId.HasValue
				? data.ServiceHandoffs.FirstOrDefault(x => x.Id == item.ServiceHandoffId.Value)
				: null;
			item.Operation = new AdmissionCrmOperationQueueContextDto
			{
				QueueType = queueType,
				Severity = item.IsUrgent ? "Urgent" : item.AgeDays > 0 ? "Attention" : "Normal",
				AssignmentStatus = item.OwnerUserId.HasValue ? "Assigned" : "Unassigned",
				ServiceHandoffStatus = handoff?.Status,
				RetryAttemptCount = handoff?.AttemptCount
			};
		}
	}
}