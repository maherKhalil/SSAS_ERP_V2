using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

string sqlFile = @"scripts\HIS_Migration.sql";
string outDir = @"src\Modules\HIS\SSAS.HIS.Domain\Entities";

var schemasToScaffold = new[] { "BloodBank", "Emergency", "Pharmacy" };

if (!Directory.Exists(outDir)) {
    Directory.CreateDirectory(outDir);
}

string sql = File.ReadAllText(sqlFile);

var insertRegex = new Regex(@"INSERT INTO \[(?<schema>[^\]]+)\]\.\[(?<table>[^\]]+)\] \((?<columns>[^\)]+)\)", RegexOptions.Singleline | RegexOptions.Multiline);
var matches = insertRegex.Matches(sql);

// to map SQL types to C#, but we don't have SQL types here. We only have column names!
// Let's assume Id is int/long or Guid based on convention. If there's an 'Id' field, we can make it an int? Or Guid? Let's use string if not sure, but maybe we can make them all `public string {get;set;}` or `int` for IDs. Let's make them strings as a safe fallback, or dynamic? 
// The task just says "generate EF Core entities... MUST implement ITenantOwnedEntity ... include public Guid TenantId { get; set; }"
// Let's use `public Guid TenantId { get; set; }` and for all other columns, let's use `public string? ColName { get; set; }` since we don't have types. 
// Oh wait, if `Id` is there, maybe EF core will complain if it's string without key?
// Let's check a typical entity in the domain to see if it inherits a base entity.

