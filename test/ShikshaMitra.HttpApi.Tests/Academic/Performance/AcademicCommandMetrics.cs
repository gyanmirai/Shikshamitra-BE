using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace ShikshaMitra.Academic.Performance;

/// <summary>Per-request reader metrics, enabled only by the opt-in synthetic benchmark.</summary>
public sealed class AcademicCommandMetrics : DbCommandInterceptor
{
	private readonly object _gate = new();
	private List<CommandMeasurement>? _active;

	public void Begin()
	{
		lock (_gate) _active = [];
	}

	public List<CommandMeasurement> End()
	{
		lock (_gate)
		{
			var result = _active ?? [];
			_active = null;
			return result;
		}
	}

	public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
		=> Observe(command, eventData, result);

	public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
		DbDataReader result, CancellationToken cancellationToken = default)
		=> ValueTask.FromResult(Observe(command, eventData, result));

	private DbDataReader Observe(DbCommand command, CommandExecutedEventData eventData, DbDataReader reader)
	{
		lock (_gate)
		{
			if (_active == null) return reader;
			var measurement = new CommandMeasurement(command.CommandText, eventData.Duration.TotalMilliseconds,
				command.Parameters.Cast<NpgsqlParameter>().Select(parameter => parameter.Clone()).ToArray());
			_active.Add(measurement);
			return new CountingReader(reader, measurement);
		}
	}

	public sealed class CommandMeasurement(string sql, double executeMilliseconds, NpgsqlParameter[] parameters)
	{
		public string Sql { get; } = sql;
		public double ExecuteMilliseconds { get; } = executeMilliseconds;
		public long RowsRead;
		public NpgsqlParameter[] Parameters { get; } = parameters;
	}

	private sealed class CountingReader(DbDataReader inner, CommandMeasurement measurement) : DbDataReader
	{
		public override bool Read()
		{
			var result = inner.Read();
			if (result) Interlocked.Increment(ref measurement.RowsRead);
			return result;
		}
		public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
		{
			var result = await inner.ReadAsync(cancellationToken);
			if (result) Interlocked.Increment(ref measurement.RowsRead);
			return result;
		}
		public override object this[int ordinal] => inner[ordinal];
		public override object this[string name] => inner[name];
		public override int Depth => inner.Depth;
		public override int FieldCount => inner.FieldCount;
		public override bool HasRows => inner.HasRows;
		public override bool IsClosed => inner.IsClosed;
		public override int RecordsAffected => inner.RecordsAffected;
		public override int VisibleFieldCount => inner.VisibleFieldCount;
		public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
		public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
		public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
		public override char GetChar(int ordinal) => inner.GetChar(ordinal);
		public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
		public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
		public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
		public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
		public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
		public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
		public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
		public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
		public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
		public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
		public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
		public override string GetName(int ordinal) => inner.GetName(ordinal);
		public override int GetOrdinal(string name) => inner.GetOrdinal(name);
		public override string GetString(int ordinal) => inner.GetString(ordinal);
		public override object GetValue(int ordinal) => inner.GetValue(ordinal);
		public override int GetValues(object[] values) => inner.GetValues(values);
		public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
		public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => inner.IsDBNullAsync(ordinal, cancellationToken);
		public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);
		public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => inner.GetFieldValueAsync<T>(ordinal, cancellationToken);
		public override bool NextResult() => inner.NextResult();
		public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => inner.NextResultAsync(cancellationToken);
		public override DataTable? GetSchemaTable() => inner.GetSchemaTable();
		public override IEnumerator GetEnumerator() => new DbEnumerator(this, false);
		public override void Close() => inner.Close();
		public override Task CloseAsync() => inner.CloseAsync();
		protected override void Dispose(bool disposing)
		{
			if (disposing) inner.Dispose();
			base.Dispose(disposing);
		}
		public override ValueTask DisposeAsync() => inner.DisposeAsync();
	}
}
