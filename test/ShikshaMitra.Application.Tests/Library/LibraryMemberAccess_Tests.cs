using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Members;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryMemberAccess_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Borrowed_Books_Should_Reject_Unknown_Member_Instead_Of_Returning_Authorized_Looking_Empty_List()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            GetRequiredService<ILibraryMemberAppService>().GetBorrowedBooksAsync(Guid.NewGuid()));

        exception.Code.ShouldBe(LibraryErrorCodes.MemberNotFound);
    }
}
