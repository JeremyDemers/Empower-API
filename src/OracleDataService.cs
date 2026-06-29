using Oracle.ManagedDataAccess.Client;

public class OracleDataService
{

    public string ConnectionString { get; private set; }

    public OracleDataService(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("OracleConnection");
    }


    public List<string> GetFolderNames()
    {
        var folderNames = new List<string>();
        using (var conn = new OracleConnection(ConnectionString))
        {
            conn.Open();
            var query = @"
                SELECT DISTINCT SCHEMA
                FROM MILLENNIUM.PROJECTINFO
                WHERE NAME LIKE '%Data Acquisition\PSSM\GRO\GRO ' || TO_CHAR(SYSDATE, 'YYYY') || '\%'";


            if (DateTime.Now.Month <= 3)
            {
                query += @"
                    UNION
                    SELECT DISTINCT SCHEMA
                    FROM MILLENNIUM.PROJECTINFO
                    WHERE NAME LIKE '%Data Acquisition\PSSM\GRO\GRO ' || (TO_CHAR(SYSDATE, 'YYYY') + 1) || '\%'";
            }

            using (var cmd = new OracleCommand(query, conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        folderNames.Add(reader.GetString(0));
                    }
                }
            }
        }
        return folderNames;
    }

}
