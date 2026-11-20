using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalLoanApi_Tests
{
	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View + "," + ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.Add + "," + ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View + "," + ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public async Task Receiving_Http_Options_Should_Require_Its_Exact_Actions(string permissions)
	{
		using var limited = LimitedClient(permissions);
		using var response = await limited.GetAsync(ReceivingRoute + "/options");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Receiving_Http_Options_Should_Expose_Destinations_To_A_Receiving_Operator_Without_Catalogue_Write_Access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = await ReceivingInputAsync();
		using var receiver = LimitedClient(string.Join(',', ShikshaMitraSubmodulePermissions.Library.Receiving.View,
			ShikshaMitraSubmodulePermissions.Library.Receiving.Add, ShikshaMitraSubmodulePermissions.Library.AllBooks.View));
		using var response = await receiver.GetAsync(ReceivingRoute + "/options");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var options = await response.Content.ReadFromJsonAsync<LibraryReceivingOptionsDto>(JsonOptions);
		options.ShouldNotBeNull();
		options.Sections.ShouldContain(section => section.Id == input.Lines[0].SectionId &&
			section.ShelfDetails.Exists(shelf => shelf.Id == input.Lines[0].ShelfId));
	}
}
