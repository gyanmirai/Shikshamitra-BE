using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public partial class BookIssue
{
	public Guid? BorrowerScopeCaptureId { get; private set; }
	public int? BorrowerScopeCaptureVersion { get; private set; }
	public LibraryBorrowerScopeProvenance BorrowerScopeProvenance { get; private set; }

	public void BindStaffScopeCapture(LibraryBorrowerScopeCapture capture)
	{
		if (MemberRole != 1 || BorrowerScopeCaptureId.HasValue || capture.Provenance != LibraryBorrowerScopeProvenance.CapturedAtOperation)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		capture.EnsureSource(TenantId, LibraryBorrowerScopeSourceKind.Loan, Id, MemberId, AcademicYearId);
		BorrowerScopeCaptureId = capture.Id; BorrowerScopeCaptureVersion = capture.Version; BorrowerScopeProvenance = capture.Provenance;
	}
}
