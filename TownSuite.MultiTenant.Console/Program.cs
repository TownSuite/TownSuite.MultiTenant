﻿using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TownSuite.MultiTenant;
using TownSuite.MultiTenant.Console;

// See https://aka.ms/new-console-template for more information

string settingsFile = "appsettings.json";
if (args.Length <= 1)
{
    PrintHelp();
    Environment.Exit(0);
}

string sql = string.Empty;
string appPattern = string.Empty;
string outputFile = "output.csv";
for (int i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--settingsfile"))
    {
        settingsFile = args[i + 1];
    }
    else if (args[i] == "--sql")
    {
        sql = args[i + 1];
    }
    else if (args[i] == "--apppattern")
    {
        appPattern = args[i + 1];
    }
    else if (args[i] == "--outputfile")
    {
        outputFile = args[i + 1];
    }
    else if (args[i] == "/?" || args[i] == "-help" || args[i] == "help" || args[i] == "--help")
    {
        PrintHelp();
        Environment.Exit(0);
    }
}

if (string.IsNullOrWhiteSpace(sql))
{
    Console.WriteLine("--sql option is required");
    Environment.Exit(0);
}

if (string.IsNullOrWhiteSpace(appPattern))
{
    Console.WriteLine("--apppattern option is required");
    Environment.Exit(0);
}

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var builder = new ConfigurationBuilder()
    .AddJsonFile(settingsFile, false, true)
    .AddJsonFile($"appsettings.{environment}.json", true, true)
    .AddEnvironmentVariables();
var configurationRoot = builder.Build();

var settings = configurationRoot.GetSection("AppSettings").Get<AppSettings>();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
        .AddConsole();
});
var loggerResolver = loggerFactory.CreateLogger<TenantResolver>();
var loggerReader = loggerFactory.CreateLogger<HttpConfigReader>();
var logger = loggerFactory.CreateLogger<Program>();

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = Environment.NewLine,
};

using var writer = new StreamWriter("path\\to\\file.csv");
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);


foreach (var lookup in settings.ConfigPairs)
{
    var resolver = new TenantResolver(loggerResolver,
        new HttpConfigReader(loggerReader, new UniqueIdRetriever(lookup.SqlUniqueIdLookup),
            new TsWebClient(new HttpClient(), lookup.ConfigReaderUrlBearerToken, settings.UserAgent),
            new Settings()
            {
                ConfigReaderUrls = lookup.ConfigReaderUrl,
                DecryptionKey = lookup.DecryptionKey,
                UniqueIdDbPattern = lookup.UniqueIdDbPattern
            }));
    resolver.Clear();
    await resolver.ResolveAll();

    foreach (var tenant in resolver.Tenants)
    {
        try
        {
            // See the centralconfig for appNames
            await using var conn = tenant.Value.CreateConnection("WebService");
            await conn.OpenAsync();
            var records = await conn.QueryAsync<dynamic>(sql);

            csv.WriteRecords(records);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve for tenant {tenant}", tenant.Key);
        }
    }
}

static void PrintHelp()
{
    Console.WriteLine("Required options");
    Console.WriteLine("--settingsfile \"/path/to/appsettings.json\"");
    Console.WriteLine("--sql \"SELECT TOP 10 FROM exampletable\"");
    Console.WriteLine("--apppattern \"*.appType\"");
    Console.WriteLine(
        "    The type of app to retrieve the connection string for.  Each tenant can have multiple app types.");
    Console.WriteLine("--output \"C:\\a\\folder\\output.csv\"");
    Console.WriteLine("");
    Console.WriteLine("Examples");
    Console.WriteLine("");
    Console.WriteLine(
        ".\\TownSuite.MultiTenant.Console.exe --settingsfile \"C:\\a\\folder\\appsettings.json\" --sql \"SELECT TOP 10 FROM exampletable\" --apppattern \"*.Example\" --output \"C:\\a\\folder\\output.csv\"");
}