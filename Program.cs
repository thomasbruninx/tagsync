using System;
using System.IO;
using System.Collections.Generic;
using Serilog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog.Configuration;

public class Tag{
    public string ID { get; set; } = "";    // NVARCHAR(50)
    public string Naam { get; set; } = "";    // NVARCHAR(255)
}

public class DatabaseService
{
    private string connectionString;

    public DatabaseService(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public bool TestConnection()
    {
        // Test de verbinding met de database
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Log.Information("Database connection successful.");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Database connection failed: {ex.Message}");
            return false;
        }
        return true;
    }

    public void InsertTags(List<Tag> tags)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (var tag in tags)
                {
                    string sql = "INSERT INTO tblVerpleging (ID, Naam) VALUES (@ID, @Naam)";
                    SqlCommand command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@ID", tag.ID);
                    command.Parameters.AddWithValue("@Naam", tag.Naam);
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Database insert failed: {ex.Message}");
        }
    }

    public void UpdateTags(List<Tag> tags)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (var tag in tags)
                {
                    string sql = "UPDATE tblVerpleging SET Naam = @Naam WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@ID", tag.ID);
                    command.Parameters.AddWithValue("@Naam", tag.Naam);
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Database update failed: {ex.Message}");
        }
    }

    public List<Tag> GetAllTags()
    {
        var items = new List<Tag>();

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = "SELECT ID, Naam FROM tblVerpleging";
                SqlCommand command = new SqlCommand(sql, connection);

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new Tag
                        {
                            ID = reader["ID"]?.ToString() ?? "",
                            Naam = reader["Naam"]?.ToString() ?? ""
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Database connection failed: {ex.Message}");
        }

        return items;
    }
}

class Program
{
    static List<Tag> parseTagsCsv(string csvPath, char delimiter = ';', bool hasHeaders = true)
    {
        var lines = File.ReadLines(csvPath).Skip(hasHeaders ? 1 : 0);
        List<Tag> tags = lines.Select(line => {
            var parts = line.Split(delimiter);
            return new Tag {
                ID = parts[0].Trim(),
                Naam = parts[1].Trim()
            };
        }).ToList();
        return tags;
    }

    static void SyncTags(DatabaseService dbService, string csvPath, char csvDelimiter = ';', bool csvHasHeaders = true)
    {
        // Load tags from CSV
        var csvTags = parseTagsCsv(csvPath, csvDelimiter, csvHasHeaders);

        // Get existing tags from database
        var dbTags = dbService.GetAllTags();

        // Determine which tags to insert or update
        var tagsToInsert = new List<Tag>();
        var tagsToUpdate = new List<Tag>();

        foreach (var csvTag in csvTags)
        {
            var existingTag = dbTags.Find(t => t.ID == csvTag.ID);
            if (existingTag == null)
            {
                tagsToInsert.Add(csvTag);
            }
            else if (existingTag.Naam != csvTag.Naam)
            {
                tagsToUpdate.Add(csvTag);
            }
        }

        // Insert new tags
        if (tagsToInsert.Count > 0)
        {
            dbService.InsertTags(tagsToInsert);
            Log.Information($"Inserted {tagsToInsert.Count} new tags.");
        }

        // Update existing tags
        if (tagsToUpdate.Count > 0)
        {
            dbService.UpdateTags(tagsToUpdate);
            Log.Information($"Updated {tagsToUpdate.Count} existing tags.");
        }
    }

    static void Main(string[] args)
    {
        // Default configuration values
        string connectionString = "";
        string directoryToWatch = @"C:\temp";
        string fileFilter = "tags.csv";
        char csvDelimiter = ';';
        bool csvHasHeaders = true;

        // Register Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Year)
            .CreateLogger();

        // Load configuration from config.ini
        try 
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddIniFile("config.ini", optional: false, reloadOnChange: false);

            IConfiguration config = configBuilder.Build();

            connectionString = config["Novilog:ConnectionString"] ?? "";
            directoryToWatch = config["Input:Directory"] ?? @"C:\temp";
            fileFilter = config["Input:Filter"] ?? "tags.csv";
            csvDelimiter = config["Input:CsvDelimiter"] != null ? config["Input:CsvDelimiter"]?[0] ?? ';' : ';';
            csvHasHeaders = config["Input:CsvHasHeaders"] != null ? bool.Parse(config["Input:CsvHasHeaders"] ?? "true") : true;
        }
        catch (FileNotFoundException ex)
        {
            Log.Fatal($"Configuration file not found: {ex.FileName}");
            return;
        }
        catch (Exception ex)
        {
            Log.Fatal($"Error: {ex.Message}");
            return;
        }
        
        // Test database connection
        if (string.IsNullOrEmpty(connectionString))
        {
            Log.Fatal("Connection string is not set. Please check your config.ini.");
            return;
        }

        DatabaseService dbService = new DatabaseService(connectionString);
        if (!dbService.TestConnection())
        {
            Log.Fatal("Unable to connect to the database. Please check your connection string.");
            return;
        }

        // Register file system watcher
        using var watcher = new FileSystemWatcher(directoryToWatch);
        watcher.Filter = fileFilter;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        watcher.Changed += (s, e) => SyncTags(dbService, e.FullPath, csvDelimiter, csvHasHeaders);
        watcher.Created += (s, e) => SyncTags(dbService, e.FullPath, csvDelimiter, csvHasHeaders);
        watcher.EnableRaisingEvents = true;

        // Keep the application running
        while (true)
        {
            Console.WriteLine("Watching for file changes. Press 'q' to quit.");
            if (Console.ReadKey().KeyChar == 'q') break;
        }

    }
}