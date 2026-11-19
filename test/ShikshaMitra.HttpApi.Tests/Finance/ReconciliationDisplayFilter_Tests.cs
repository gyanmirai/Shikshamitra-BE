using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using Xunit;
namespace ShikshaMitra.Finance;
public class ReconciliationDisplayFilter_Tests
{
 [Theory]
 [InlineData(PaymentMethodEnum.BankTransfer, "accountHolderName", "accountHolderName")]
 [InlineData(PaymentMethodEnum.CreditCard, "cardHolderName", "cardHolderName")]
 [InlineData(PaymentMethodEnum.DebitCard, "cardHolderName", "cardHolderName")]
 public void Missing_holder_matches_visible_dash_but_empty_text_stays_empty(PaymentMethodEnum method, string column, string key)
 {
  var missing = new ReconciliationListItemDto { PaymentMethod = method };
  var explicitNull = new ReconciliationListItemDto { PaymentMethod = method, InstrumentDetails = new() { [key] = null } };
  var empty = new ReconciliationListItemDto { PaymentMethod = method, InstrumentDetails = new() { [key] = "" } };
  var named = new ReconciliationListItemDto { PaymentMethod = method, InstrumentDetails = new() { [key] = "Header Holder" } };
  var rows = new[] { missing, explicitNull, empty, named }.AsQueryable();
  Assert.Equal(new[] { missing, explicitNull }, Apply(rows, method, column, "-"));
  Assert.Equal(new[] { empty }, Apply(rows, method, column, ""));
  Assert.Equal(new[] { named }, Apply(rows, method, column, "header holder"));
  Assert.Empty(Apply(rows, method, column, "absent"));
 }
 [Theory]
 [InlineData(PaymentMethodEnum.CreditCard)]
 [InlineData(PaymentMethodEnum.DebitCard)]
 public void Missing_or_empty_card_digits_match_visible_dash_and_preserve_mask(PaymentMethodEnum method)
 {
  var missing = new ReconciliationListItemDto { PaymentMethod = method };
  var empty = new ReconciliationListItemDto { PaymentMethod = method, InstrumentDetails = new() { ["cardLast4"] = "" } };
  var named = new ReconciliationListItemDto { PaymentMethod = method, InstrumentDetails = new() { ["cardLast4"] = "12340042" } };
  var rows = new[] { missing, empty, named }.AsQueryable();
  Assert.Equal(new[] { missing, empty }, Apply(rows, method, "last4Digits", "-"));
  Assert.Equal(new[] { named }, Apply(rows, method, "last4Digits", "•••• 0042"));
 }
 private static ReconciliationListItemDto[] Apply(IQueryable<ReconciliationListItemDto> rows, PaymentMethodEnum method, string column, string value)
 {
  // Exercise the production allowlist and expression pipeline without starting a database host.
  var service = typeof(ReconciliationAppService);
  var fields = (IReadOnlyDictionary<string,LambdaExpression>)service.GetField("ListFilterFields", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
  var json = JsonSerializer.Serialize(new[] { new { column, type = "equals", value } });
  var query = (FinanceTableQuery)service.GetMethod("ParseListFilters", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object?[] { json, method })!;
  return query.Apply(rows, fields).ToArray();
 }
}
