using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerFetchAllInstruments
    {
        public static List<Dictionary<string, object>> FetchAllInstruments(string connectionString, DatabaseService databaseService, int year)
        {
            var schemas = databaseService.GetSchemasByYear(year);
            var combinedData = new List<Dictionary<string, object>>();
            var uniqueEntries = new HashSet<string>();

            foreach (var schema in schemas)
            {
                if (SchemaHasTables(connectionString, schema)) // Check if schema has tables
                {
                    var schemaData = FetchDataForSchema(connectionString, schema);
                    foreach (var entry in schemaData)
                    {
                        var entryKey = string.Join(",", entry.Values.Select(v => v?.ToString() ?? "null"));
                        if (uniqueEntries.Add(entryKey))
                        {
                            combinedData.Add(entry);
                        }
                    }
                }
            }

            return combinedData;
        }

        private static bool SchemaHasTables(string connectionString, string schema)
        {
            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM all_tables WHERE owner = '{schema}'";
                var tableCount = Convert.ToInt32(command.ExecuteScalar());
                return tableCount > 0;
            }
        }

        private static List<Dictionary<string, object>> FetchDataForSchema(string connectionString, string schema)
        {
            var data = new List<Dictionary<string, object>>();
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var query = $@"
                SELECT CHRO.NAME INSTRUMENT, CHRO.NODE SERVER, CHRO.COMMENTS
                FROM {schema}.CHROMSYSCONFIG CHRO";
            using (var cmd = new OracleCommand(query, conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dict = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            if (reader.GetName(i) == "COMMENTS" && value != null)
                            {
                                value = value.ToString().Replace("\r", "").Replace("\n", "");
                            }
                            dict.Add(reader.GetName(i), value);
                        }
                        data.Add(dict);
                    }
                }
            }

            return data;
        }
    }
}
