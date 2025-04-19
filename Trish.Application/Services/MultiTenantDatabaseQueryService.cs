using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Npgsql;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class MultiTenantDatabaseQueryService
    {
        private readonly IPostgresTenantConnectionManager _tenantConnectionManager;
        private readonly IChatCompletionService _ai;
        private readonly ITenantSchemaRegistry _schemaRegistry;
        private readonly ILogger<MultiTenantDatabaseQueryService> _logger;

        public MultiTenantDatabaseQueryService(IPostgresTenantConnectionManager tenantConnectionManager,
                                               IChatCompletionService ai,
                                               ITenantSchemaRegistry schemaRegistry,
                                               ILogger<MultiTenantDatabaseQueryService> logger)
        {
            _tenantConnectionManager = tenantConnectionManager;
            _ai = ai;
            _schemaRegistry = schemaRegistry;
            _logger = logger;
        }

        public async Task<string> ExecuteNaturalLanguageQueryAsync(string tenantId, string question)
        {
            string schemaDefinition = await _schemaRegistry.GetTenantSchemaDefinitionAsync(tenantId);

            string sqlQuery = await GenerateSqlFromQuestionAsync(question, schemaDefinition);

            using var connection = await _tenantConnectionManager.GetConnectionForTenantAsync(tenantId);

            try
            {
                var result = await connection.QueryAsync(sqlQuery);
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query: {SqlQuery}", sqlQuery);
                throw new Exception("Error executing SQL query", ex);
            }
        }

        private async Task<string> GenerateSqlFromQuestionAsync(string question, string schemaDefinition)
        {
            var chat = new ChatHistory();

            chat.AddSystemMessage($@"
             You are a SQL expert that converts natural language questions to SQL queries for PostgreSQL.
            Only return the SQL query without any explanation.
            Use the schema definition below to create accurate queries.
            For safety, limit results to 100 records maximum.
            
            {schemaDefinition}
            ");

            chat.AddUserMessage($"Question: {question}\nSQL:");

            var response = await _ai.GetChatMessageContentAsync(chat);
            string sql = response.Content.Trim();

            _logger.LogInformation("Generated SQL: {Sql}", sql);

            return sql;
        }

        public async Task<string> UploadCsvToTableAsync(
            string tenantId,
            string tableName,
            Stream csvStream,
            bool hasHeaderRow = true,
            char delimiter = ',')
        {
            _logger.LogInformation("Starting CSV upload for tenant {TenantId} to table {TableName}", tenantId, tableName);

            // Sanitize table name to prevent SQL injection
            string safeTblName = SanitizeTableName(tableName);
            if (safeTblName != tableName)
            {
                _logger.LogWarning("Table name was sanitized from {TableName} to {SafeTableName}", tableName, safeTblName);
                tableName = safeTblName;
            }

            using var connection = await _tenantConnectionManager.GetConnectionForTenantAsync(tenantId) as NpgsqlConnection;
            if (connection == null)
            {
                throw new InvalidOperationException("Could not get PostgreSQL connection for tenant");
            }

            // Parse the CSV to determine structure
            List<string> headers = new List<string>();
            List<List<string>> records = new List<List<string>>();
            Dictionary<string, PostgresDataType> columnTypes = new Dictionary<string, PostgresDataType>();

            await ParseCsvAndInferTypes(csvStream, hasHeaderRow, delimiter, headers, records, columnTypes);

            // Create table if it doesn't exist
            bool tableExists = await TableExists(connection, tableName);
            if (!tableExists)
            {
                await CreateTable(connection, tableName, headers, columnTypes);
                _logger.LogInformation("Created new table {TableName}", tableName);
            }
            else
            {
                _logger.LogInformation("Table {TableName} already exists, verifying schema compatibility", tableName);
                // Verify schema compatibility
                await VerifyTableSchema(connection, tableName, headers, columnTypes);
            }

            // Insert data
            int insertedRows = await InsertDataIntoTable(connection, tableName, headers, records);

            return JsonSerializer.Serialize(new
            {
                message = $"Successfully uploaded CSV data to table '{tableName}'",
                tableCreated = !tableExists,
                rowsInserted = insertedRows,
                columns = headers
            });
        }

        private async Task ParseCsvAndInferTypes(
            Stream csvStream,
            bool hasHeaderRow,
            char delimiter,
            List<string> headers,
            List<List<string>> records,
            Dictionary<string, PostgresDataType> columnTypes)
        {
            csvStream.Position = 0;
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = hasHeaderRow,
                Delimiter = delimiter.ToString(),
                MissingFieldFound = null
            });

            // Read header if present
            if (hasHeaderRow)
            {
                csv.Read();
                csv.ReadHeader();
                headers.AddRange(csv.HeaderRecord.Select(SanitizeColumnName));
            }

            // Read records
            int rowIndex = 0;
            int columnCount = 0;

            while (csv.Read())
            {
                if (rowIndex == 0 && !hasHeaderRow)
                {
                    // For files without headers, create generic column names
                    columnCount = csv.Parser.Count;
                    for (int i = 0; i < columnCount; i++)
                    {
                        headers.Add(SanitizeColumnName($"column_{i + 1}"));
                    }
                }

                var record = new List<string>();
                for (int i = 0; i < (hasHeaderRow ? headers.Count : columnCount); i++)
                {
                    string value = csv.GetField(i) ?? string.Empty;
                    record.Add(value);

                    // Infer column types
                    string columnName = headers[i];
                    PostgresDataType inferredType = InferPostgresType(value);

                    if (!columnTypes.ContainsKey(columnName))
                    {
                        columnTypes[columnName] = inferredType;
                    }
                    else if (columnTypes[columnName] != inferredType)
                    {
                        // If current type is more specific but new value requires more general type
                        if (inferredType == PostgresDataType.Text ||
                            (columnTypes[columnName] == PostgresDataType.Integer && inferredType == PostgresDataType.Numeric))
                        {
                            columnTypes[columnName] = inferredType;
                        }
                    }
                }

                records.Add(record);
                rowIndex++;

                // Sample up to 1000 rows for type inference
                if (rowIndex >= 1000) break;
            }

            // Continue reading the rest of the records if needed
            if (rowIndex < 1000)
            {
                while (csv.Read())
                {
                    var record = new List<string>();
                    for (int i = 0; i < (hasHeaderRow ? headers.Count : columnCount); i++)
                    {
                        record.Add(csv.GetField(i) ?? string.Empty);
                    }
                    records.Add(record);
                }
            }

            // Default any column without a determined type to text
            foreach (var column in headers)
            {
                if (!columnTypes.ContainsKey(column))
                {
                    columnTypes[column] = PostgresDataType.Text;
                }
            }
        }

        private async Task<bool> TableExists(NpgsqlConnection connection, string tableName)
        {
            string sql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = @tableName
                );";

            return await connection.ExecuteScalarAsync<bool>(sql, new { tableName });
        }

        private async Task CreateTable(
            NpgsqlConnection connection,
            string tableName,
            List<string> columns,
            Dictionary<string, PostgresDataType> columnTypes)
        {
            StringBuilder createTableSql = new StringBuilder();
            createTableSql.AppendLine($"CREATE TABLE \"{tableName}\" (");

            for (int i = 0; i < columns.Count; i++)
            {
                string columnName = columns[i];
                string columnType = GetPostgresTypeString(columnTypes[columnName]);

                createTableSql.Append($"\"{columnName}\" {columnType}");

                if (i < columns.Count - 1)
                {
                    createTableSql.AppendLine(",");
                }
                else
                {
                    createTableSql.AppendLine();
                }
            }

            createTableSql.Append(");");

            await connection.ExecuteAsync(createTableSql.ToString());
        }

        private async Task VerifyTableSchema(
            NpgsqlConnection connection,
            string tableName,
            List<string> csvColumns,
            Dictionary<string, PostgresDataType> csvColumnTypes)
        {
            string sql = @"
                SELECT column_name, data_type, udt_name
                FROM information_schema.columns
                WHERE table_schema = 'public' 
                AND table_name = @tableName;";

            var existingColumns = await connection.QueryAsync(sql, new { tableName });
            var dbColumns = existingColumns.ToDictionary(
                row => row.column_name.ToString(),
                row => GetDataTypeFromDb(row.data_type.ToString(), row.udt_name.ToString())
            );

            List<string> missingColumns = new List<string>();
            Dictionary<string, (PostgresDataType csvType, PostgresDataType dbType)> incompatibleColumns =
                new Dictionary<string, (PostgresDataType, PostgresDataType)>();

            foreach (var csvColumn in csvColumns)
            {
                if (!dbColumns.ContainsKey(csvColumn))
                {
                    missingColumns.Add(csvColumn);
                }
                else if (!AreTypesCompatible(csvColumnTypes[csvColumn], dbColumns[csvColumn]))
                {
                    incompatibleColumns.Add(csvColumn, (csvColumnTypes[csvColumn], dbColumns[csvColumn]));
                }
            }

            // Add missing columns if any
            if (missingColumns.Any())
            {
                _logger.LogInformation("Adding {Count} missing columns to table {TableName}", missingColumns.Count, tableName);

                foreach (var column in missingColumns)
                {
                    string alterSql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{column}\" {GetPostgresTypeString(csvColumnTypes[column])};";
                    await connection.ExecuteAsync(alterSql);
                }
            }

            // Handle incompatible columns if any
            if (incompatibleColumns.Any())
            {
                _logger.LogWarning("Found {Count} incompatible column types in table {TableName}", incompatibleColumns.Count, tableName);

                foreach (var kvp in incompatibleColumns)
                {
                    string column = kvp.Key;
                    var (csvType, dbType) = kvp.Value;

                    // Only attempt to alter column type if CSV type is more general
                    if (IsTypeMoreGeneral(csvType, dbType))
                    {
                        string alterSql = $"ALTER TABLE \"{tableName}\" ALTER COLUMN \"{column}\" TYPE {GetPostgresTypeString(csvType)} USING \"{column}\"::{GetPostgresTypeString(csvType)};";
                        try
                        {
                            await connection.ExecuteAsync(alterSql);
                            _logger.LogInformation("Altered column {Column} from {OldType} to {NewType}",
                                column, GetPostgresTypeString(dbType), GetPostgresTypeString(csvType));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to alter column {Column} type", column);
                            throw new InvalidOperationException($"Column '{column}' has incompatible types and cannot be automatically converted", ex);
                        }
                    }
                }
            }
        }

        private async Task<int> InsertDataIntoTable(
            NpgsqlConnection connection,
            string tableName,
            List<string> columns,
            List<List<string>> records)
        {
            if (!records.Any())
            {
                return 0;
            }

            // Use PostgreSQL's COPY command for fast bulk loading
            using (var writer = connection.BeginTextImport($"COPY \"{tableName}\" (\"{string.Join("\", \"", columns)}\") FROM STDIN WITH (FORMAT TEXT)"))
            {
                foreach (var record in records)
                {
                    StringBuilder line = new StringBuilder();

                    for (int i = 0; i < record.Count; i++)
                    {
                        string value = record[i];

                        if (string.IsNullOrEmpty(value))
                        {
                            line.Append("\\N"); // NULL value in PostgreSQL COPY
                        }
                        else
                        {
                            // Escape special characters
                            value = value.Replace("\\", "\\\\")
                                         .Replace("\t", "\\t")
                                         .Replace("\n", "\\n")
                                         .Replace("\r", "\\r");

                            line.Append(value);
                        }

                        if (i < record.Count - 1)
                        {
                            line.Append("\t"); // Tab separator for COPY
                        }
                    }

                    writer.WriteLine(line.ToString());
                }
            }

            return records.Count;
        }

        private string SanitizeTableName(string tableName)
        {
            // Remove any non-alphanumeric characters except underscores
            string sanitized = new string(tableName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

            // Ensure it starts with a letter
            if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]))
            {
                sanitized = "t_" + sanitized;
            }

            // If empty after sanitization, use a default name
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "imported_table";
            }

            return sanitized.ToLower();
        }

        private string SanitizeColumnName(string columnName)
        {
            // Remove any non-alphanumeric characters except underscores
            string sanitized = new string(columnName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

            // Ensure it starts with a letter
            if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]))
            {
                sanitized = "c_" + sanitized;
            }

            // If empty after sanitization, use a default name
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "column";
            }

            return sanitized.ToLower();
        }

        private enum PostgresDataType
        {
            Integer,
            Numeric,
            Boolean,
            Date,
            Timestamp,
            Text
        }

        private PostgresDataType InferPostgresType(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return PostgresDataType.Text; // Default for empty values
            }

            // Try boolean
            if (bool.TryParse(value, out _) ||
                value.Equals("t", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("f", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return PostgresDataType.Boolean;
            }

            // Try integer
            if (int.TryParse(value, out _))
            {
                return PostgresDataType.Integer;
            }

            // Try numeric
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                return PostgresDataType.Numeric;
            }

            // Try date (ISO format)
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return PostgresDataType.Date;
            }

            // Try timestamp
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return PostgresDataType.Timestamp;
            }

            // Default to text
            return PostgresDataType.Text;
        }

        private string GetPostgresTypeString(PostgresDataType dataType)
        {
            return dataType switch
            {
                PostgresDataType.Integer => "INTEGER",
                PostgresDataType.Numeric => "NUMERIC",
                PostgresDataType.Boolean => "BOOLEAN",
                PostgresDataType.Date => "DATE",
                PostgresDataType.Timestamp => "TIMESTAMP",
                PostgresDataType.Text => "TEXT",
                _ => "TEXT"
            };
        }

        private PostgresDataType GetDataTypeFromDb(string dataType, string udtName)
        {
            string type = dataType.ToLowerInvariant();
            string udt = udtName.ToLowerInvariant();

            if (type == "integer" || udt == "int4")
            {
                return PostgresDataType.Integer;
            }

            if (type.Contains("numeric") || type.Contains("decimal") ||
                udt == "numeric" || udt == "float4" || udt == "float8")
            {
                return PostgresDataType.Numeric;
            }

            if (type == "boolean" || udt == "bool")
            {
                return PostgresDataType.Boolean;
            }

            if (type == "date" || udt == "date")
            {
                return PostgresDataType.Date;
            }

            if (type.Contains("timestamp") || udt.Contains("timestamp"))
            {
                return PostgresDataType.Timestamp;
            }

            return PostgresDataType.Text;
        }

        private bool AreTypesCompatible(PostgresDataType csvType, PostgresDataType dbType)
        {
            if (csvType == dbType)
            {
                return true;
            }

            // Integer can be stored in Numeric
            if (csvType == PostgresDataType.Integer && dbType == PostgresDataType.Numeric)
            {
                return true;
            }

            // Date can be stored in Timestamp
            if (csvType == PostgresDataType.Date && dbType == PostgresDataType.Timestamp)
            {
                return true;
            }

            // Almost anything can be stored as text
            if (dbType == PostgresDataType.Text)
            {
                return true;
            }

            return false;
        }

        private bool IsTypeMoreGeneral(PostgresDataType type1, PostgresDataType type2)
        {
            // Text is most general
            if (type1 == PostgresDataType.Text && type2 != PostgresDataType.Text)
            {
                return true;
            }

            // Numeric is more general than Integer
            if (type1 == PostgresDataType.Numeric && type2 == PostgresDataType.Integer)
            {
                return true;
            }

            // Timestamp is more general than Date
            if (type1 == PostgresDataType.Timestamp && type2 == PostgresDataType.Date)
            {
                return true;
            }

            return false;
        }

    }
}
