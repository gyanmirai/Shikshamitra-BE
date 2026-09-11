using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	private static readonly JsonSerializerOptions PatchJson = new(JsonSerializerDefaults.Web);
	private static BusinessException InvalidPatch(string message) => new BusinessException(FeeStructureErrorCodes.InvalidConfiguration).WithData("Errors", new[] { message });

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task<FeeStructureCreateResponse> PatchAsync(Guid id, FeeStructurePatchRequest input)
	{
		var entity = await GetScopedAsync(id);
		if (input.RequestId == Guid.Empty || input.Changes.ValueKind != JsonValueKind.Object) throw InvalidPatch("A request ID and explicit changes are required.");
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Changes.GetRawText())));
		var receipts = JsonSerializer.Deserialize<Dictionary<Guid, string>>(entity.GetProperty<string>("PatchRequests") ?? "{}")!;
		if (receipts.TryGetValue(input.RequestId, out var existing))
		{
			if (existing != hash) throw new BusinessException(FeeStructureErrorCodes.ConcurrencyConflict);
			return await MapAsync(entity);
		}
		entity.EnsureNotLocked();
		await EnsureUnusedAsync(id);
		EnsureStamp(entity, input.ConcurrencyStamp);
		var before = ToRequest(entity);
		StandardFeeStructureRequest candidate;
		try { candidate = MergePatch(before, input.Changes); }
		catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException or NullReferenceException)
		{ throw InvalidPatch("The changed fields have an invalid value or shape."); }
		if (JsonSerializer.Serialize(before, PatchJson) == JsonSerializer.Serialize(candidate, PatchJson)) return await MapAsync(entity);
		if (OnlyNameOrAmountChanges(input.Changes))
		{
			var shape = new StandardFeeStructureRequestValidator().Validate(candidate);
			if (!shape.IsValid) throw InvalidPatch(string.Join("\n", shape.Errors.Select(x => x.ErrorMessage)));
		}
		else ThrowIfInvalid(await ValidateCandidateAsync(entity.FeeStructureMetadataId, entity.StandardId, candidate, entity, false));
		await CheckAttachmentsAsync(candidate, entity);
		await EnsureUniqueNameAsync(entity.StandardId, candidate.Name, entity.Id, entity.RevisionRootId);
		ApplyCandidate(entity, candidate);
		receipts[input.RequestId] = hash;
		entity.SetProperty("PatchRequests", JsonSerializer.Serialize(receipts));
		await feeStructureRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	private static bool OnlyNameOrAmountChanges(JsonElement changes)
	{
		foreach (var property in changes.EnumerateObject())
		{
			if (property.Name == "name") continue;
			if (property.Name != "feeHeads") return false;
			foreach (var operation in property.Value.EnumerateObject())
			{
				if (operation.Name != "update") { if (operation.Value.GetArrayLength() != 0) return false; continue; }
				if (operation.Value.EnumerateArray().Any(line => line.EnumerateObject().Any(field => field.Name is not ("lineId" or "amount")))) return false;
			}
		}
		return true;
	}

	private static StandardFeeStructureRequest MergePatch(StandardFeeStructureRequest saved, JsonElement changes)
	{
		var target = JsonSerializer.SerializeToNode(saved, PatchJson)!.AsObject();
		var patch = JsonNode.Parse(changes.GetRawText())!.AsObject();
		Allowed(patch, "name", "policies", "feeHeads");
		if (patch.TryGetPropertyValue("name", out var name)) target["name"] = name?.DeepClone();
		if (patch.TryGetPropertyValue("policies", out var policyNode))
		{
			var policies = policyNode?.AsObject() ?? throw InvalidPatch("Policies must be an object.");
			Allowed(policies, "collection", "concession", "proration", "refund");
			foreach (var (family, value) in policies)
			{
				var selection = value?.AsObject();
				if (selection != null)
				{
					Allowed(selection, family == "concession" ? new[] { "policyId", "policyVersionId", "feeHeadScope", "applicableFeeHeadIds" } : new[] { "policyId", "policyVersionId" });
					if (selection["policyId"] == null || selection["policyVersionId"] == null || selection["policyId"]!.GetValue<Guid>() == Guid.Empty || selection["policyVersionId"]!.GetValue<Guid>() == Guid.Empty) throw InvalidPatch("An attachment requires both policy and immutable version IDs.");
				}
				target[family + "PolicyId"] = selection?["policyId"]?.DeepClone();
				target[family + "PolicyVersionId"] = selection?["policyVersionId"]?.DeepClone();
				if (family == "concession")
				{
					if (selection == null) { target["concessionFeeHeadScope"] = "EntireFeeStructure"; target["concessionApplicableFeeHeadIds"] = new JsonArray(); }
					else
					{
						if (selection.TryGetPropertyValue("feeHeadScope", out var scope)) target["concessionFeeHeadScope"] = scope?.DeepClone();
						if (selection.TryGetPropertyValue("applicableFeeHeadIds", out var ids)) target["concessionApplicableFeeHeadIds"] = ids?.DeepClone();
					}
				}
			}
		}
		if (patch.TryGetPropertyValue("feeHeads", out var headNode))
		{
			var operations = headNode?.AsObject() ?? throw InvalidPatch("Fee Head changes must be an object.");
			Allowed(operations, "add", "update", "removeLineIds");
			var lines = target["feeHeads"]!.AsArray();
			var touched = new HashSet<Guid>();
			foreach (var removed in Array(operations, "removeLineIds"))
			{
				var lineId = removed!.GetValue<Guid>();
				if (!touched.Add(lineId)) throw InvalidPatch("A Fee Head line can be changed only once per request.");
				var line = lines.SingleOrDefault(x => x?["id"]?.GetValue<Guid>() == lineId) ?? throw InvalidPatch("The removed line does not belong to this structure.");
				lines.Remove(line);
			}
			foreach (var update in Array(operations, "update"))
			{
				var fields = update!.AsObject();
				Allowed(fields, "lineId", "amount", "isOneTime", "isOptional", "payableMonth", "payableYear", "schedule");
				var lineId = fields["lineId"]!.GetValue<Guid>();
				if (!touched.Add(lineId)) throw InvalidPatch("A Fee Head line can be changed only once per request.");
				var line = lines.SingleOrDefault(x => x?["id"]?.GetValue<Guid>() == lineId)?.AsObject() ?? throw InvalidPatch("The updated line does not belong to this structure.");
				foreach (var (field, value) in fields.Where(x => x.Key is not ("lineId" or "schedule"))) line[field] = value?.DeepClone();
				if (fields.TryGetPropertyValue("schedule", out var schedule))
				{
					var values = schedule?.AsObject() ?? throw InvalidPatch("Use an explicit inherited schedule to clear customization.");
					Allowed(values, "paymentTermId", "paymentTermVersionId", "scheduleMode", "installmentOverrides");
					if (values.Count != 4) throw InvalidPatch("A schedule change must include term, version, mode and overrides together.");
					foreach (var (field, value) in values) line[field] = value?.DeepClone();
				}
			}
			foreach (var addition in Array(operations, "add"))
			{
				var line = addition!.AsObject();
				Allowed(line, "feeHeadId", "amount", "isOneTime", "isOptional", "payableMonth", "payableYear", "paymentTermId", "paymentTermVersionId", "scheduleMode", "installmentOverrides");
				lines.Add(line.DeepClone());
			}
		}
		return target.Deserialize<StandardFeeStructureRequest>(PatchJson) ?? throw InvalidPatch("Invalid configuration changes.");
	}

	private static IEnumerable<JsonNode?> Array(JsonObject value, string name) => !value.ContainsKey(name) ? [] :
		value[name]?.AsArray() ?? throw InvalidPatch($"{name} must be an array.");
	private static void Allowed(JsonObject value, params string[] names)
	{
		if (value.Any(x => !names.Contains(x.Key))) throw InvalidPatch("The request contains a field that cannot be edited here.");
	}
}
