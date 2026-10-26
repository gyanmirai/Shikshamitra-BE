using System;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class ConcessionRequestLifecycle_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Explicit_administrator_creator_decision_preserves_actor_and_terminal_state(bool approve)
	{
		var request = CreateDraft();
		var creator = Guid.NewGuid();
		typeof(ConcessionRequest).GetProperty(nameof(ConcessionRequest.CreatorId))!.SetValue(request, creator);
		var now = DateTime.UtcNow;
		if (approve)
		{
			request.PrepareSubmissionEvidence([Guid.NewGuid()], "{\"inputs\":[]}", "[]");
			request.Submit(now);
			request.Approve(creator, "Administrator reviewed own request", [], now, allowCreatorDecision: true);
			Assert.Equal(ConcessionRequestStatus.Approved, request.Status);
		}
		else
		{
			request.Reject(creator, "Administrator rejected own incomplete request", now, allowCreatorDecision: true);
			Assert.Equal(ConcessionRequestStatus.Rejected, request.Status);
			Assert.Null(request.SubmittedOn);
		}
		Assert.Equal(creator, request.GetApprovedBy());
		Assert.Equal(now, request.GetApprovedOn());
		Assert.Throws<BusinessException>(() => request.Reject(creator, "Cannot decide an already decided request", now, allowCreatorDecision: true));
	}

	[Fact]
	public void Incomplete_saved_request_can_be_rejected_without_submission()
	{
		var request = CreateDraft();
		var creator = Guid.NewGuid();
		typeof(ConcessionRequest).GetProperty(nameof(ConcessionRequest.CreatorId))!.SetValue(request, creator);
		var checker = Guid.NewGuid();
		var now = DateTime.UtcNow;
		Assert.Throws<BusinessException>(() => request.Reject(creator, "Cannot decide own request", now));
		Assert.Throws<BusinessException>(() => request.Reject(checker, "short", now));
		Assert.Equal(ConcessionRequestStatus.Draft, request.Status);

		request.Reject(checker, "Incomplete request is no longer required", now);

		Assert.Equal(ConcessionRequestStatus.Rejected, request.Status);
		Assert.Null(request.SubmittedOn);
		Assert.Equal(checker, request.GetApprovedBy());
		Assert.Empty(request.ConcessionDueFeeAllocations);
		Assert.Throws<BusinessException>(() => request.Reject(checker, "Already decided request", now));
	}
	[Fact]
	public void Administrator_creation_transition_requires_the_original_creator()
	{
		var request = CreateDraft();
		var creator = Guid.NewGuid();
		typeof(ConcessionRequest).GetProperty(nameof(ConcessionRequest.CreatorId))!
			.SetValue(request, creator);
		request.PrepareSubmissionEvidence([Guid.NewGuid()], "{\"inputs\":[]}", "[]");
		request.Submit(DateTime.UtcNow);

		Assert.Throws<BusinessException>(() =>
			request.Approve(creator, null, [], DateTime.UtcNow));
		Assert.Throws<BusinessException>(() =>
			request.ApproveOnAdministratorCreation(Guid.NewGuid(), null, [], DateTime.UtcNow));

		request.ApproveOnAdministratorCreation(creator, "Automatically approved", [], DateTime.UtcNow);
		Assert.Equal(ConcessionRequestStatus.Approved, request.Status);
		Assert.Equal(creator, request.GetApprovedBy());
	}

	[Fact]
	public void Submission_freezes_draft_and_cancel_preserves_reason_and_actor()
	{
		var request = CreateDraft();
		var dueFeeId = Guid.NewGuid();
		var submittedOn = DateTime.UtcNow;
		var cancelledOn = submittedOn.AddMinutes(1);
		var actorId = Guid.NewGuid();

		request.PrepareSubmissionEvidence([dueFeeId], "{\"inputs\":[]}", "[]");
		request.Submit(submittedOn);

		Assert.Equal(ConcessionRequestStatus.Pending, request.Status);
		Assert.Equal(submittedOn, request.SubmittedOn);
		Assert.Contains(dueFeeId, request.GetSelectedDueFeeIds());
		Assert.Throws<BusinessException>(() => request.UpdateRemarks("changed after submission"));

		request.Cancel(actorId, "Applicant withdrew this request", cancelledOn);

		Assert.Equal(ConcessionRequestStatus.Cancelled, request.Status);
		Assert.Equal("Applicant withdrew this request", request.CancellationReason);
		Assert.Equal(actorId, request.CancelledBy);
		Assert.Equal(cancelledOn, request.CancelledOn);
	}

	[Fact]
	public void Cancellation_rejects_short_reason_and_terminal_state()
	{
		var request = CreateDraft();
		Assert.Throws<BusinessException>(() => request.Cancel(Guid.NewGuid(), "short", DateTime.UtcNow));

		request.Cancel(Guid.NewGuid(), "Draft is no longer required", DateTime.UtcNow);
		Assert.Throws<BusinessException>(() =>
			request.Cancel(Guid.NewGuid(), "Trying to cancel it again", DateTime.UtcNow));
	}

	[Fact]
	public void Incomplete_draft_can_clear_provisional_submission_evidence()
	{
		var request = CreateDraft();
		request.PrepareSubmissionEvidence([Guid.NewGuid()], "[{\"amount\":0}]", "[]");

		request.ClearSubmissionEvidence();

		Assert.Empty(request.GetSelectedDueFeeIds());
		Assert.Null(request.RequestInputSnapshotJson);
		Assert.Null(request.TypeRequirementsSnapshotJson);
		Assert.Throws<BusinessException>(() => request.Submit(DateTime.UtcNow));
	}

	[Fact]
	public void Staged_document_can_only_attach_to_matching_owner_requirement_and_request()
	{
		var ownerId = Guid.NewGuid();
		var typeId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var requestId = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var upload = new ConcessionDocumentUpload(
			Guid.NewGuid(), Guid.NewGuid(), typeId, documentId, ownerId,
			"/api/files/evidence.pdf", now.AddHours(24));

		upload.Attach(requestId, ownerId, typeId, documentId, now);

		Assert.Equal(requestId, upload.ConcessionRequestId);
		Assert.Throws<BusinessException>(() =>
			upload.Attach(Guid.NewGuid(), ownerId, typeId, documentId, now));
		Assert.Throws<BusinessException>(() =>
			upload.Attach(requestId, Guid.NewGuid(), typeId, documentId, now));
	}

	[Fact]
	public void Expired_staged_document_cannot_be_attached()
	{
		var now = DateTime.UtcNow;
		var upload = new ConcessionDocumentUpload(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"/api/files/evidence.pdf", now);

		var exception = Assert.Throws<BusinessException>(() => upload.Attach(
			Guid.NewGuid(), upload.UploadedBy, upload.ConcessionTypeId, upload.DocumentId, now));

		Assert.Equal(FinanceConcessionErrorCodes.StagedDocumentExpired, exception.Code);
	}

	[Fact]
	public void Removed_draft_document_is_released_for_cleanup_and_cannot_be_reused()
	{
		var now = DateTime.UtcNow;
		var ownerId = Guid.NewGuid();
		var typeId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var requestId = Guid.NewGuid();
		var upload = new ConcessionDocumentUpload(
			Guid.NewGuid(), Guid.NewGuid(), typeId, documentId, ownerId,
			"/api/files/evidence.pdf", now.AddHours(24));
		upload.Attach(requestId, ownerId, typeId, documentId, now);

		upload.ReleaseFromDraft(requestId, now.AddMinutes(1));

		Assert.Null(upload.ConcessionRequestId);
		Assert.Null(upload.AttachedOn);
		Assert.Equal(now.AddMinutes(1), upload.ExpiresOn);
		Assert.Throws<BusinessException>(() =>
			upload.Attach(requestId, ownerId, typeId, documentId, now.AddMinutes(1)));
	}

	[Fact]
	public void Replacing_existing_document_updates_tracked_row_without_duplicate()
	{
		var request = CreateDraft();
		var documentId = Guid.NewGuid();
		var existing = new ConcessionRequestDocument(
			Guid.NewGuid(), request.TenantId, request.Id, documentId, "/api/files/original.pdf", 1);
		request.ReplaceDocuments([existing]);

		var replacement = new ConcessionRequestDocument(
			Guid.NewGuid(), request.TenantId, request.Id, documentId, "/api/files/replacement.pdf", 1);
		request.ReplaceDocuments([replacement]);

		var saved = Assert.Single(request.ConcessionRequestDocuments);
		Assert.Same(existing, saved);
		Assert.Equal("/api/files/replacement.pdf", saved.Url);
		Assert.Equal(1, saved.Status);
	}

	[Fact]
	public void Replacing_documents_removes_stale_rows_and_adds_new_requirements()
	{
		var request = CreateDraft();
		var staleDocumentId = Guid.NewGuid();
		request.ReplaceDocuments([
			new ConcessionRequestDocument(
				Guid.NewGuid(), request.TenantId, request.Id, staleDocumentId, "/api/files/stale.pdf", 1)
		]);

		var newDocumentId = Guid.NewGuid();
		request.ReplaceDocuments([
			new ConcessionRequestDocument(
				Guid.NewGuid(), request.TenantId, request.Id, newDocumentId, "/api/files/new.pdf", 1)
		]);

		var saved = Assert.Single(request.ConcessionRequestDocuments);
		Assert.Equal(newDocumentId, saved.DocumentId);
		Assert.DoesNotContain(request.ConcessionRequestDocuments, x => x.DocumentId == staleDocumentId);
	}

	private static ConcessionRequest CreateDraft() => new(
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		Guid.NewGuid(),
		"Draft request",
		ConcessionRequestStatus.Draft);
}
