using Microsoft.EntityFrameworkCore;

public class ResultsCheckContext : DbContext
{
    public DbSet<Folder> Folders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use the SQLite connection string from the configuration
        optionsBuilder.UseSqlite("Data Source=resultsCheck.db");
    }
}

