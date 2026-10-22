using ShikshaMitra.Common.Middleware;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public class MobileSubmissionErrorMapping_Tests
{
	[Theory]
	[InlineData(MobileSubmissionErrorCodes.SubmissionConflict)]
	[InlineData(MobileSubmissionErrorCodes.IdempotencyConflict)]
	public void Problem_details_preserve_submission_conflicts(string code)
	{
		Assert.Equal(409, ApiProblemDetailsMapper.ResolveBusinessStatusCode(code));
		Assert.Equal(409, ApiProblemDetailsMapper.NormalizeStatusCode(400, code));
	}
}
