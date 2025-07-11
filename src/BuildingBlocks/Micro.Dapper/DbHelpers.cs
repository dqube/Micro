using System.Collections;
using System.Data;
using System.Reflection;

namespace Micro.Dapper;

public static class DbHelpers
{
    #region Query Methods (Unified for both SQL and Stored Procedures)

    public static IEnumerable<T> Query<T>(
        this IDbConnection connection,
        string sqlOrProcedure,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        int? commandTimeout = null,
        CommandType? commandType = null)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) connection.Open();

        try
        {
            using (var cmd = CreateCommand(connection, sqlOrProcedure, param, transaction, commandTimeout, commandType))
            using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | (buffered ? CommandBehavior.Default : CommandBehavior.SingleResult)))
            {
                var result = ReadData<T>(reader);
                if (wasClosed && buffered) connection.Close();
                return result;
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    public static async Task<IEnumerable<T>> QueryAsync<T>(
        this IDbConnection connection,
        string sqlOrProcedure,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        var wasClosed = connection.State == ConnectionState.Closed;
        // IDbConnection does not have OpenAsync, so use synchronous Open
        if (wasClosed) connection.Open();

        try
        {
            using (var cmd = CreateCommand(connection, sqlOrProcedure, param, transaction, commandTimeout, commandType))
            {
                // IDbCommand does not have ExecuteReaderAsync, so use synchronous ExecuteReader
                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    var result = await ReadDataAsync<T>(reader);
                    if (wasClosed) connection.Close();
                    return result;
                }
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    #endregion

    #region Execute Methods (Unified for both SQL and Stored Procedures)

    public static int Execute(
        this IDbConnection connection,
        string sqlOrProcedure,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) connection.Open();

        try
        {
            using (var cmd = CreateCommand(connection, sqlOrProcedure, param, transaction, commandTimeout, commandType))
            {
                return cmd.ExecuteNonQuery();
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    public static async Task<int> ExecuteAsync(
        this IDbConnection connection,
        string sqlOrProcedure,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        var wasClosed = connection.State == ConnectionState.Closed;
        // IDbConnection does not have OpenAsync, so use synchronous Open
        if (wasClosed) connection.Open();

        try
        {
            using (var cmd = CreateCommand(connection, sqlOrProcedure, param, transaction, commandTimeout, commandType))
            {
                // IDbCommand does not have ExecuteNonQueryAsync, so use synchronous ExecuteNonQuery
                return await Task.FromResult(cmd.ExecuteNonQuery());
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    #endregion

    #region Output Parameter Support (For Stored Procedures)

    public static TOut? GetOutputParameter<TOut>(
        this IDbCommand command,
        string parameterName)
    {
        var param = command.Parameters.Cast<IDbDataParameter>()
            .FirstOrDefault(p => p.Direction.HasFlag(ParameterDirection.Output) &&
                               p.ParameterName.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

        if (param == null || param.Value == DBNull.Value)
            return default;

        return (TOut?)ConvertSimpleType(param.Value, typeof(TOut));
    }

    public static Dictionary<string, object?> GetAllOutputParameters(
        this IDbCommand command)
    {
        return command.Parameters.Cast<IDbDataParameter>()
            .Where(p => p.Direction.HasFlag(ParameterDirection.Output))
            .ToDictionary(
                p => p.ParameterName,
                p => p.Value == DBNull.Value ? null : p.Value
            );
    }

    #endregion

    #region Helper Methods

    private static IDbCommand CreateCommand(
        IDbConnection connection,
        string sqlOrProcedure,
        object? param,
        IDbTransaction? transaction,
        int? commandTimeout,
        CommandType? commandType)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = sqlOrProcedure;
        cmd.Transaction = transaction;

        if (commandTimeout.HasValue)
            cmd.CommandTimeout = commandTimeout.Value;

        // Auto-detect command type if not specified
        if (commandType == null)
        {
            cmd.CommandType = sqlOrProcedure.Trim().Contains(' ') ?
                CommandType.Text : CommandType.StoredProcedure;
        }
        else
        {
            cmd.CommandType = commandType.Value;
        }

        AddParameters(cmd, param);

        return cmd;
    }

    private static void AddParameters(IDbCommand command, object? param)
    {
        if (param == null) return;

        if (param is IEnumerable<KeyValuePair<string, object?>> dictionaryParams)
        {
            foreach (var kvp in dictionaryParams)
            {
                AddParameter(command, kvp.Key, kvp.Value);
            }
        }
        else if (param is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                AddParameter(command, key, entry.Value);
            }
        }
        else
        {
            var properties = param.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in properties)
            {
                AddParameter(command, prop.Name, prop.GetValue(param));
            }
        }
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();

        // Handle parameter name conventions
        if (!name.StartsWith("@") && command.CommandType == CommandType.StoredProcedure)
        {
            parameter.ParameterName = "@" + name;
        }
        else
        {
            parameter.ParameterName = name;
        }

        parameter.Value = value ?? DBNull.Value;

        // Special handling for output parameters
        if (name.StartsWith("Output_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("@Output_", StringComparison.OrdinalIgnoreCase))
        {
            parameter.Direction = ParameterDirection.Output;

            // Set appropriate size for output parameters
            if (value == null)
            {
                parameter.Size = GetDefaultSizeForType(parameter.DbType);
            }
        }

        command.Parameters.Add(parameter);
    }

    private static int GetDefaultSizeForType(DbType dbType)
    {
        switch (dbType)
        {
            case DbType.String:
            case DbType.AnsiString:
            case DbType.StringFixedLength:
            case DbType.AnsiStringFixedLength:
                return 4000;
            case DbType.Decimal:
                return 18;
            default:
                return 0;
        }
    }

    #endregion

    #region Auto-Mapping Implementation

    private static IEnumerable<T> ReadData<T>(IDataReader reader)
    {
        var type = typeof(T);

        if (type.IsSimpleType())
        {
            while (reader.Read())
            {
                yield return (T?)ConvertSimpleType(reader.GetValue(0), type)!;
            }
            yield break;
        }

        var properties = GetSettableProperties(type);
        var columns = GetColumnNames(reader);
        var propertyMap = BuildPropertyMap(properties, columns);

        while (reader.Read())
        {
            var instance = Activator.CreateInstance<T>();
            for (int i = 0; i < columns.Length; i++)
            {
                if (propertyMap.TryGetValue(columns[i], out var property) && !reader.IsDBNull(i))
                {
                    var value = reader.GetValue(i);
                    property.SetValue(instance, ConvertValue(value, property.PropertyType));
                }
            }
            yield return instance;
        }
    }

    private static async Task<IEnumerable<T>> ReadDataAsync<T>(IDataReader reader)
    {
        var type = typeof(T);
        var results = new List<T>();

        if (type.IsSimpleType())
        {
            while (await Task.Run(() => reader.Read()))
            {
                results.Add((T?)ConvertSimpleType(reader.GetValue(0), type)!);
            }
            return results;
        }

        var properties = GetSettableProperties(type);
        var columns = GetColumnNames(reader);
        var propertyMap = BuildPropertyMap(properties, columns);

        while (await Task.Run(() => reader.Read()))
        {
            var instance = Activator.CreateInstance<T>();
            for (int i = 0; i < columns.Length; i++)
            {
                if (propertyMap.TryGetValue(columns[i], out var property) && !reader.IsDBNull(i))
                {
                    var value = reader.GetValue(i);
                    property.SetValue(instance, ConvertValue(value, property.PropertyType));
                }
            }
            results.Add(instance);
        }

        return results;
    }

    private static bool IsSimpleType(this Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               (Nullable.GetUnderlyingType(type) is Type underlying && underlying.IsSimpleType());
    }

    private static PropertyInfo[] GetSettableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite && p.GetSetMethod(nonPublic: false) != null)
            .ToArray();
    }

    private static string[] GetColumnNames(IDataReader reader)
    {
        return Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToArray();
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap(PropertyInfo[] properties, string[] columns)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            // Exact match first
            var property = properties.FirstOrDefault(p => string.Equals(p.Name, column, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                map[column] = property;
                continue;
            }

            // Handle underscore_case to PascalCase conversion
            var pascalCaseColumn = ToPascalCase(column);
            property = properties.FirstOrDefault(p => string.Equals(p.Name, pascalCaseColumn, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                map[column] = property;
            }
        }

        return map;
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var parts = s.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", parts.Select(p => p.Substring(0, 1).ToUpper() + p.Substring(1).ToLower()));
    }

    private static object? ConvertSimpleType(object? value, Type targetType)
    {
        if (value == null || value is DBNull)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying == null)
                return null;
            targetType = underlying;
        }

        return Convert.ChangeType(value, targetType);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null || value is DBNull)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                if (underlyingType.IsEnum)
                {
                    return Enum.ToObject(underlyingType, value);
                }
                return Convert.ChangeType(value, underlyingType);
            }
            return null;
        }

        return Convert.ChangeType(value, targetType);
    }

    #endregion
}