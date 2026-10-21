using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeConfigurationPermissionAuthorization_Tests
{
	[Theory]
	[InlineData(typeof(FeeStructureAppService), "PublishAsync", "Finance.Settings.FeeStructure.Add")]
	[InlineData(typeof(ReceiptBookAppService), "GetListAsync", "Finance.Settings.ReceiptBook.View")]
	[InlineData(typeof(ReceiptBookAppService), "GetAsync", "Finance.Settings.ReceiptBook.View")]
	[InlineData(typeof(ReceiptBookAppService), "CreateAsync", "Finance.Settings.ReceiptBook.Add")]
	[InlineData(typeof(ReceiptBookAppService), "UpdateAsync", "Finance.Settings.ReceiptBook.Add")]
	[InlineData(typeof(ReceiptBookAppService), "UpdateStatusAsync", "Finance.Settings.ReceiptBook.Add")]
	[InlineData(typeof(ReceiptBookAppService), "DeleteAsync", "Finance.Settings.ReceiptBook.Delete")]
	[InlineData(typeof(FeeStructureAppService), "CreateRevisionAsync", "Finance.Settings.FeeStructure.Add")]
	[InlineData(typeof(FeeStructureAppService), "UpdateAsync", "Finance.Settings.FeeStructure.Add")]
	[InlineData(typeof(CollectionPolicyAppService), "UpdateAsync", "Finance.Policies.CollectionPolicy.Add")]
	[InlineData(typeof(ConcessionPolicyAppService), "UpdateAsync", "Finance.Policies.ConcessionPolicy.Add")]
	[InlineData(typeof(RefundPolicyAppService), "UpdateAsync", "Finance.Policies.RefundPolicy.Add")]
	[InlineData(typeof(ProrationPolicyAppService), "UpdateAsync", "Finance.Policies.ProrationPolicy.Add")]
	[InlineData(typeof(ConcessionPolicyAppService), "ArchiveAsync", "Finance.Policies.ConcessionPolicy.Delete")]
	[InlineData(typeof(RefundPolicyAppService), "ArchiveAsync", "Finance.Policies.RefundPolicy.Delete")]
	[InlineData(typeof(CollectionPolicyAppService), "ArchiveAsync", "Finance.Policies.CollectionPolicy.Delete")]
	[InlineData(typeof(ProrationPolicyAppService), "ArchiveAsync", "Finance.Policies.ProrationPolicy.Delete")]
	[InlineData(typeof(ConcessionPolicyAppService), "GetVersionsAsync", "Finance.Policies.ConcessionPolicy.View")]
	public void Configuration_Endpoints_Should_Require_The_Canonical_Action(Type service, string method, string expected)
	{
		var policies = service.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ToArray();
		policies.ShouldBe(new[] { expected });
	}
}
