using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace ShikshaMitra.Library.Shared;

public static class LibraryBorrowerScopeEvidenceGuard
{
	public static void Validate(DbContext context)
	{
		var captures = context.ChangeTracker.Entries<LibraryBorrowerScopeCapture>().ToArray();
		var children = context.ChangeTracker.Entries<LibraryBorrowerAssignmentSnapshot>().ToArray();
		if (captures.Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
			children.Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
			throw new InvalidOperationException("Recorded Library borrower scope evidence cannot be changed or deleted after it is saved.");
		var addedCaptures = captures.Where(entry => entry.State == EntityState.Added)
			.ToDictionary(entry => (entry.Entity.TenantId, entry.Entity.Id), entry => entry.Entity);
		var addedChildren = children.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToArray();
		foreach (var child in addedChildren)
			if (!child.TenantId.HasValue || !addedCaptures.ContainsKey((child.TenantId, child.CaptureId)))
				throw new BusinessException(LibraryErrorCodes.ContextInvalid, "Assignment evidence must be saved with its new capture. Create a reviewed capture to record a new assignment set.");
		foreach (var capture in addedCaptures.Values)
			if (!capture.TenantId.HasValue || capture.ExpectedAssignmentCount < 0 ||
				addedChildren.Count(child => child.TenantId == capture.TenantId && child.CaptureId == capture.Id) != capture.ExpectedAssignmentCount)
				throw new BusinessException(LibraryErrorCodes.ContextInvalid, "Save the complete recorded assignment set with its new capture.");
	}
}
