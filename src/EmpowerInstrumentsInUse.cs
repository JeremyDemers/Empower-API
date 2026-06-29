using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerInstrumentsInUse
    {
        public static List<Dictionary<string, object>> FetchInstrumentsInUse(string connectionString, DatabaseService databaseService, int year)
        {
            var schemas = databaseService.GetSchemasByYear(year);
            var combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                if (SchemaHasTables(connectionString, schema))
                {
                    combinedData.AddRange(FetchDataForSchema(connectionString, schema));
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
                SELECT
                    T2.USERID,
                    T1.INSTRUMENT, T1.SERVER, T1.COMMENTS,
                    SAMPLE_SET_ID,
                    T1.INJ_DATE,
                    ROUND(T1.HOURS_AGO * 60, 2) SINCE_LAST,
                    ROUND(NEXT_INJ, 2) NEXT_INJ
                FROM
                    (
                    SELECT
                        INSTRUMENT, SERVER, COMMENTS,
                        TRUNC(MAX(LDATE)) INJ_DATE,
                        ROUND (((SYSDATE - TRUNC(MAX(LDATE), 'MI'))),
                        3)* 24 HOURS_AGO,
                        NEXT_INJ
                    FROM
                        (
                        SELECT
                            CHRO.NAME INSTRUMENT, CHRO.NODE SERVER, CHRO.COMMENTS,
                            INJ.DATED LDATE,
                            24 * (SYSDATE - TO_DATE(TO_CHAR(INJ.DATED, 'YYYY-MM-DD HH24:MI:SS'), 'YYYY-MM-DD HH24:MI:SS')) ELAPSED_TIME,
                            60 * ((RUNTIME / 60) - 24 * (SYSDATE - TO_DATE(TO_CHAR(INJ.DATED, 'YYYY-MM-DD HH24:MI:SS'), 'YYYY-MM-DD HH24:MI:SS'))) NEXT_INJ
                        FROM
                            {schema}.SAMPLESET SS,
                            {schema}.CHROMSYSCONFIG CHRO,
                            {schema}.INJECTION INJ
                        WHERE
                            CHRO.ID = INJ.CONFIG_ID
                            AND SS.ID = INJ.SS_ID
                            AND INJ.DATED > SYSDATE - 42)
                    GROUP BY
                        INSTRUMENT, SERVER, COMMENTS,NEXT_INJ) T1,
                    (
                    SELECT
                        USERID,
                        INSTRUMENT, SERVER, COMMENTS,
                        SAMPLE_SET_ID,
                        TRUNC(MAX(LDATE)) INJ_DATE,
                        ROUND (((SYSDATE - TRUNC(MAX(LDATE), 'MI'))),
                        3)* 24 HOURS_AGO
                    FROM
                        (
                        SELECT
                            SS.ACQUIRED_BY$ USERID,
                            CHRO.NAME INSTRUMENT,  CHRO.NODE SERVER, CHRO.COMMENTS,
                            SS.ID SAMPLE_SET_ID,
                            MAX(INJ.DATED) LDATE
                        FROM
                            {schema}.SAMPLESET SS,
                            {schema}.CHROMSYSCONFIG CHRO,
                            {schema}.INJECTION INJ
                        WHERE
                            CHRO.ID = INJ.CONFIG_ID
                            AND SS.ID = INJ.SS_ID
                            AND INJ.DATED > SYSDATE - 42
                        GROUP BY
                            SS.ACQUIRED_BY$,
                            CHRO.NAME, CHRO.NODE, CHRO.COMMENTS,
                            SS.ID)
                    GROUP BY
                        USERID, INSTRUMENT, COMMENTS, SERVER, SAMPLE_SET_ID
                ) T2
                WHERE
                    T1.INSTRUMENT = T2.INSTRUMENT
                    AND T1.INJ_DATE = T2.INJ_DATE
                    AND T1.HOURS_AGO = T2.HOURS_AGO
                    AND NEXT_INJ > - 10
                ORDER BY
                    T1.INSTRUMENT,
                    T1.HOURS_AGO
            ";
            using (var cmd = new OracleCommand(query, conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dict = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            dict.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                        }
                        data.Add(dict);
                    }
                }
            }

            return data;
        }
    }
}