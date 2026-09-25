using System;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionRequestDueFeeEvidence_Tests
{
	[Fact]
	public void Submission_pins_each_requested_due_fee_amount()
	{
		var dueId = Guid.NewGuid();
		var headId = Guid.NewGuid();
		var request = new ConcessionRequest(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, ConcessionRequestStatus.Draft);
		request.PrepareSubmissionEvidence([dueId],
			JsonSerializer.Serialize(new[] { new DueFeeConcessionEvidence(dueId, headId, 10m, 25m) }),
			"[]");

		request.Submit(DateTime.UtcNow);

		Assert.Contains(dueId, request.GetSelectedDueFeeIds());
		var line = Assert.Single(ConcessionRequestManager.ReadDueFeeEvidence(request));
		Assert.Equal(10m, line.Amount);
		Assert.Equal(25m, line.RequestedAmount);
	}
}
