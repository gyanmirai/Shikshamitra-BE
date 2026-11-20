using System;
using System.Text.Json;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportScopeSignature_Tests
{
	[Fact]
	public void Signature_Ignores_Ordering_Duplicates_Cache_Time_And_Recipient_Identity()
	{
		var a = Guid.NewGuid();
		var b = Guid.NewGuid();
		var scope = new LibraryReportExecutionScope
		{
			TenantId = Guid.NewGuid(), AllowedAcademicYearIds = [a, b, a],
			UserScope = new() { UserId = Guid.NewGuid(), AcademicYearIds = [a, b], BoardIds = [a, b], HasWildcardBoard = true, WildcardBoardAcademicYearIds = [a] }
		};
		var equivalent = JsonSerializer.Deserialize<LibraryReportExecutionScope>(JsonSerializer.Serialize(scope))!;
		equivalent.AllowedAcademicYearIds = [b, a];
		equivalent.UserScope.AcademicYearIds = [b, a, b];
		equivalent.UserScope.BoardIds = [b, a];
		equivalent.UserScope.UserId = Guid.NewGuid();
		equivalent.UserScope.CalculatedAt = DateTime.UtcNow.AddDays(1);
		equivalent.UserScope.FromCache = true;
		LibraryReportScopeService.CreateScopeSignature(equivalent).ShouldBe(LibraryReportScopeService.CreateScopeSignature(scope));
	}

	[Fact]
	public void Member_Signature_Ignores_Order_And_Duplicates_But_Changes_With_The_Authorized_Population()
	{
		var tenant = Guid.NewGuid();
		var a = Guid.NewGuid();
		var b = Guid.NewGuid();
		var signature = LibraryReportScopeService.CreateMemberSignature(tenant, [a, b]);
		LibraryReportScopeService.CreateMemberSignature(tenant, [b, a, b]).ShouldBe(signature);
		LibraryReportScopeService.CreateMemberSignature(tenant, [a]).ShouldNotBe(signature);
	}

	[Fact]
	public void Signature_Changes_When_A_Parent_Wildcard_Boundary_Or_Tenant_Changes()
	{
		var scope = new LibraryReportExecutionScope
		{
			TenantId = Guid.NewGuid(), AllowedAcademicYearIds = [Guid.NewGuid()],
			UserScope = new() { HasWildcardDivision = true, WildcardDivisionStandardIds = [Guid.NewGuid()] }
		};
		var signature = LibraryReportScopeService.CreateScopeSignature(scope);
		scope.UserScope.WildcardDivisionStandardIds = [Guid.NewGuid()];
		LibraryReportScopeService.CreateScopeSignature(scope).ShouldNotBe(signature);
		signature = LibraryReportScopeService.CreateScopeSignature(scope);
		scope.TenantId = Guid.NewGuid();
		LibraryReportScopeService.CreateScopeSignature(scope).ShouldNotBe(signature);
	}
}
