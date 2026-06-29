using System.Diagnostics;
using Oracle.ManagedDataAccess.Client;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class DatabaseService
{
    private readonly ResultsCheckContext _context;
    private readonly OracleDataService _oracleDataService;

    public DatabaseService(ResultsCheckContext context, OracleDataService oracleDataService)
    {
        _context = context;
        _oracleDataService = oracleDataService;
    }

    public List<string> GetSchemasWithHasDataTrue()
    {
        using var context = new ResultsCheckContext(); // Ensure this DbContext is properly configured for SQLite
        return context.Folders
                      .Where(f => f.HasData) // Select folders where HasData is true
                      .Select(f => f.Schema) // Project to get only the Schema property
                      .ToList(); // Execute the query and convert to a List
    }

    public void ClearFoldersTable()
    {
        var allFolders = _context.Folders.ToList();
        _context.Folders.RemoveRange(allFolders);
        _context.SaveChanges();
    }

    public void CreateOrUpdateDatabase()
    {
        try
        {
            _context.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating/updating the database: {ex.Message}");
        }
    }

    public void PopulateAndCheckFolders()
    {
        var folderNames = _oracleDataService.GetFolderNames();
        foreach (var folderName in folderNames)
        {
            if (!_context.Folders.Any(f => f.Schema == folderName))
            {
                _context.Folders.Add(new Folder { Schema = folderName, HasData = false });
            }
        }
        _context.SaveChanges();

        foreach (var folder in _context.Folders)
        {
            var hasData = CheckSchemaForData(folder.Schema);
            folder.HasData = hasData;
        }
        _context.SaveChanges();
    }

    public bool CheckSchemaForData(string schema)
    {
        using (var conn = new OracleConnection(_oracleDataService.ConnectionString))
        {
            conn.Open();
            var query = $@"
                SELECT COUNT(*)
                FROM {schema}.RESULTSET rs
                JOIN {schema}.SAMPLESET s ON rs.SS_ID = s.ID
                JOIN MILLENNIUM.USERINFO u ON s.ACQUIRED_BY$ = u.NAME
                WHERE TO_TIMESTAMP_TZ(rs.DATED, 'DD-MON-RR HH.MI.SS AM TZR') BETWEEN (SYSTIMESTAMP - INTERVAL '40' DAY) AND SYSTIMESTAMP";

            using (var cmd = new OracleCommand(query, conn))
            {
                var result = Convert.ToInt32(cmd.ExecuteScalar());

                return result > 0;
            }
        }
    }
    public List<string> GetSchemasByYear(int year)
    {
        try
        {
            using var conn = new OracleConnection(_oracleDataService.ConnectionString);
            conn.Open();
            var query = $@"
        SELECT DISTINCT SCHEMA
        FROM MILLENNIUM.PROJECTINFO
        WHERE NAME LIKE '%Data Acquisition\PSSM\GRO\GRO {year}\%'";

            using OracleCommand cmd = new OracleCommand(query, conn);
            var schemas = new List<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    schemas.Add(reader.GetString(0)); // SCHEMA is the first column
                }
            }
            return schemas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting schemas by year: {ex.Message}");
            return new List<string>();
        }
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}