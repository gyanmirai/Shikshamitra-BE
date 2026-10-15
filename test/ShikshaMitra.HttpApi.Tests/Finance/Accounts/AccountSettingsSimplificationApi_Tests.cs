using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Migrations;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
namespace ShikshaMitra.Finance.Accounts;
[Collection(FinanceRoutingApiTestCollection.Name)]
public class AccountSettingsSimplificationApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
 [Theory]
 [InlineData("income-categories")]
 [InlineData("expense-categories")]
 public async Task Categories_create_and_update_without_account_heads(string route)
 {
   await EnsureAcademicScopeAsync();
   var category = await PostAndReadAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}",new {name=$"Cat {Guid.NewGuid():N}",tags=new[]{"Fees"}});
   category.GetProperty("defaultAccountHeadId").ValueKind.ShouldBe(JsonValueKind.Null);
   var code=category.GetProperty("code").GetString();
   var response=await Client.PutAsJsonAsync($"/api/{route}/{category.GetProperty("id").GetGuid()}",new {
     name=$"Renamed {Guid.NewGuid():N}",tags=new[]{"Fees"},concurrencyStamp=category.GetProperty("concurrencyStamp").GetString()});
   response.EnsureSuccessStatusCode();
   var updated=await response.Content.ReadFromJsonAsync<JsonElement>();
   updated.GetProperty("code").GetString().ShouldBe(code);
   updated.GetProperty("defaultAccountHeadId").ValueKind.ShouldBe(JsonValueKind.Null);
 }
 [Fact]
 public async Task Bank_account_does_not_require_a_ledger_head()
 {
   await EnsureAcademicScopeAsync();
   var accountNumber=Random.Shared.NextInt64(100000000000,999999999999).ToString();
   var bank=await PostAndReadAsync<JsonElement>($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}",new {
     bankName="School Bank",branchName="Main",accountHolderName="School",accountType=0,accountNumber,ifscCode="SBIN0123456"});
   bank.GetProperty("accountHeadId").ValueKind.ShouldBe(JsonValueKind.Null);
   bank.GetProperty("code").GetString().ShouldNotBeNullOrWhiteSpace();
 }
 [Theory]
 [InlineData("charge","Finance.Fees.Charges.Add")]
 [InlineData("charge","Finance.Fees.Charges.Update")]
 [InlineData("collection","Finance.Fees.FeeCollections.Add")]
 [InlineData("refund","Finance.Fees.Refund.Approve")]
 public async Task Financial_picker_uses_the_owning_action_without_master_view_grants(string operation,string permission)
 {
   await EnsureAcademicScopeAsync();
   using var client=Factory.CreateClient();
   client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,permission);
   var response=await client.GetAsync($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation={operation}");
   response.EnsureSuccessStatusCode();
   var result=await response.Content.ReadFromJsonAsync<JsonElement>();
   result.GetProperty("bankAccounts").ValueKind.ShouldBe(JsonValueKind.Array);
   result.GetProperty("incomeCategories").ValueKind.ShouldBe(JsonValueKind.Array);
 }
 [Fact]
 public async Task Fee_collection_add_cannot_use_refund_approval_picker()
 {
   using var client=Factory.CreateClient();
   client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,"Finance.Fees.FeeCollections.Add");
   var response=await client.GetAsync($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation=refund");
   response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
 }
 [Fact]
 public async Task Automatic_income_preserves_payment_date_and_confirmed_corrections_are_idempotent()
 {
   await EnsureAcademicScopeAsync();
   var category=await PostAndReadAsync<JsonElement>($"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}",
     new {name=$"Auto {Guid.NewGuid():N}",tags=Array.Empty<string>()});
   var categoryId=category.GetProperty("id").GetGuid();
   var sourceId=Guid.NewGuid(); var key=$"collection:{sourceId:N}:0:0";
   var paymentDate=new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc);
   await WithTenantUowAsync(async services=> {
     var students=services.GetRequiredService<IRepository<Student,Guid>>();
     var studentId=await students.AsyncExecuter.FirstOrDefaultAsync((await students.GetQueryableAsync())
       .Where(x=>x.AcademicYearId==Factory.SeededAcademicYearId).Select(x=>x.Id));
     var manager=services.GetRequiredService<AutomaticTransactionManager>();
     for(var attempt=0;attempt<2;attempt++)
       await manager.RecordAsync(Factory.SeededAcademicYearId,categoryId,"Fees",null,null,"Receipt",12.34m,0,
         ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Income,key,sourceId,studentId,false,paymentDate);
   });
   await WithTenantUowAsync(async services=> {
     var entries=await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction,Guid>>().GetListAsync(x=>x.SourceId==sourceId);
     entries.Count.ShouldBe(1);entries[0].IsConfirmed.ShouldBeFalse();entries[0].PaymentDate.ShouldBe(paymentDate);
     await services.GetRequiredService<AutomaticTransactionManager>().ReverseAsync(sourceId);
     (await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction,Guid>>().GetListAsync(x=>x.SourceId==sourceId)).Count.ShouldBe(1);
     await services.GetRequiredService<AutomaticTransactionManager>().ConfirmAsync(sourceId);
   });
   await WithTenantUowAsync(async services=> {
     var manager=services.GetRequiredService<AutomaticTransactionManager>();
     await manager.ReverseAsync(sourceId);await manager.ReverseAsync(sourceId);
   });
   await WithTenantUowAsync(async services=> {
     var entries=await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction,Guid>>().GetListAsync(x=>x.SourceId==sourceId);
     entries.Count.ShouldBe(2);entries.Sum(x=>x.Amount).ShouldBe(0m);entries.All(x=>x.IsConfirmed).ShouldBeTrue();
     entries.All(x=>x.CategoryNameSnapshot=="Fees").ShouldBeTrue();
   });
 }
 private async Task WithTenantUowAsync(Func<IServiceProvider,Task> action)
 {
   using var scope=Factory.Services.CreateScope();var provider=scope.ServiceProvider;
   using(provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
   using(var uow=provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew:true))
   { await action(provider);await uow.CompleteAsync(); }
 }
 [Fact]
 public void Routing_migration_retains_legacy_evidence_and_keeps_manual_entries_visible()
 {
   Migration migration=new IncomeExpenseBankRouting {ActiveProvider="Npgsql.EntityFrameworkCore.PostgreSQL"};
   migration.UpOperations.OfType<DropTableOperation>().ShouldBeEmpty();
   migration.UpOperations.OfType<DropColumnOperation>().ShouldBeEmpty();
   migration.UpOperations.OfType<AddColumnOperation>().Single(x=>x.Table=="Finance_Transactions"&&x.Name=="IsConfirmed").DefaultValue.ShouldBe(true);
   migration.UpOperations.OfType<CreateIndexOperation>().Single(x=>x.Table=="Finance_Transactions"&&x.Columns.Contains("SourceKey")).IsUnique.ShouldBeTrue();
 }
 [Fact]
 public void Previous_simplification_migration_preserves_legacy_values()
 {
   Migration migration=new SimplifyAccountHeads {ActiveProvider="Npgsql.EntityFrameworkCore.PostgreSQL"};
   migration.UpOperations.First().ShouldBeOfType<SqlOperation>().Sql.ShouldContain("LegacyAccountHeadSettings20260909");
   migration.UpOperations.OfType<DropColumnOperation>().Count().ShouldBe(7);
 }
 private async Task EnsureAcademicScopeAsync()
 {
   using var scope=Factory.Services.CreateScope();
   var provider=scope.ServiceProvider;var tenantId=Guid.Parse(TestAuthHandler.TestTenantId);
   using(provider.GetRequiredService<ICurrentTenant>().Change(tenantId))
   using(var uow=provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew:true))
   {
     var students=provider.GetRequiredService<IRepository<Student,Guid>>();
     if(!await students.AnyAsync(x=>x.AcademicYearId==Factory.SeededAcademicYearId))
       await students.InsertAsync(new Student(Guid.NewGuid(),tenantId,"Accounts",null,"Test",Factory.SeededAcademicYearId,
         Factory.SeededBoardId,Factory.SeededMediumId,Factory.SeededMediumStageId,Factory.SeededStandardId,Factory.SeededDivisionId),autoSave:true);
     await uow.CompleteAsync();
   }
 }
}
