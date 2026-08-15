using System.Globalization;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Where a restored verification database's files go (ADR-022 §17, v1.2).
//
// EVERY RESTORED FILE IS RELOCATED, without exception. A backup carries the ORIGINAL database's physical
// paths, so a restore that does not redirect them writes over the very files the verification exists to
// protect — on the source server, from a job whose entire purpose is to prove those files are safe. This is
// the single most destructive mistake available in this capability, and `WITH MOVE` for every logical file
// is what prevents it.
//
// The roots come from trusted verification configuration and nowhere else. Physical names are generated per
// verification operation and per logical file, so nothing a restore writes can collide with anything
// another restore wrote — which is what makes `WITH REPLACE` unnecessary as well as prohibited.
internal static class TenantDatabaseVerificationFileLayout
{
  // SQL Server's own file-type codes from RESTORE FILELISTONLY: 'D' data, 'L' log. Anything else — a
  // FILESTREAM container, say — is not something this V1 path knows how to place.
  public const string DataFileType = "D";

  public const string LogFileType = "L";

  // Builds one MOVE target per logical file the backup contains.
  //
  // MULTIPLE DATA AND LOG FILES ARE SUPPORTED, driven entirely by what the server reports rather than by
  // assuming a single MDF/LDF pair. A database with four data files and two logs is ordinary, and a layout
  // that assumed otherwise would fail on exactly the largest databases most worth verifying.
  public static IReadOnlyList<TenantDatabaseVerificationFilePlacement> Plan(
    IReadOnlyList<TenantDatabaseBackupFileEntry> fileList,
    string verificationDatabaseName,
    string dataRoot,
    string logRoot)
  {
    ArgumentNullException.ThrowIfNull(fileList);
    ArgumentException.ThrowIfNullOrWhiteSpace(verificationDatabaseName);
    ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
    ArgumentException.ThrowIfNullOrWhiteSpace(logRoot);

    if (!TenantDatabaseVerificationNaming.IsVerificationDatabaseName(verificationDatabaseName))
    {
      // Defence in depth. The caller has already validated the name against the reserved vocabulary and the
      // registry; refusing again here means no future caller can generate a layout for a name that is not
      // demonstrably ours, and therefore cannot direct a restore at a production path.
      throw new ArgumentException(
        "Verification file layout requires a reserved verification database name.",
        nameof(verificationDatabaseName));
    }

    if (fileList.Count == 0)
    {
      throw new ArgumentException(
        "The backup file list is empty; a restore layout cannot be planned without logical file names.",
        nameof(fileList));
    }

    var placements = new List<TenantDatabaseVerificationFilePlacement>(fileList.Count);

    // ORDINAL INDEX, not a name-derived one. Logical file names come from the backup and may contain
    // characters that are illegal in a path, may repeat after normalisation, and are not under this
    // platform's control. Numbering them removes every one of those questions from the physical name.
    for (var index = 0; index < fileList.Count; index++)
    {
      var entry = fileList[index];
      var isLog = string.Equals(entry.FileType, LogFileType, StringComparison.OrdinalIgnoreCase);
      var root = isLog ? logRoot : dataRoot;
      var extension = isLog ? "ldf" : "mdf";

      var physicalName = string.Create(
        CultureInfo.InvariantCulture,
        $"{verificationDatabaseName}_{index}.{extension}");

      placements.Add(new TenantDatabaseVerificationFilePlacement(
        index,
        entry.LogicalName,
        Path.Combine(root, physicalName)));
    }

    return placements;
  }
}

// One logical file as SQL Server reported it from RESTORE FILELISTONLY. Server-derived, never caller-supplied.
internal sealed record TenantDatabaseBackupFileEntry(string LogicalName, string FileType);

// One `WITH MOVE` instruction: the server's logical name, and the verification-owned path it must be
// written to instead of the path recorded in the backup.
//
// `Index` is the file's ordinal position in the backup's file list. It names the SqlParameter carrying the
// path, so the parameter name is derived from position rather than from the logical name — which comes from
// the backup and may contain anything at all.
internal sealed record TenantDatabaseVerificationFilePlacement(
  int Index,
  string LogicalName,
  string PhysicalPath);
