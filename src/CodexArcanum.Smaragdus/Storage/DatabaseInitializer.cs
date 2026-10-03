namespace CodexArcanum.Smaragdus.Storage;

public static class DatabaseInitializer
{
    public static void Initialize(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = GetDdl();
        cmd.ExecuteNonQuery();
    }

    private static string GetDdl()
        => @"
        -- Projects
            CREATE TABLE IF NOT EXISTS Projects (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT    NOT NULL,
                Path        TEXT    NOT NULL UNIQUE,
                IndexedAt   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
            );

            -- Files
            CREATE TABLE IF NOT EXISTS Files (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId     INTEGER NOT NULL,
                Path          TEXT    NOT NULL,
                LastWriteUtc  TEXT    NOT NULL,
                FileSize      INTEGER NOT NULL,
                Hash          TEXT    NOT NULL,
                IndexedAt     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
                ReindexCount  INTEGER NOT NULL DEFAULT 0,
                IndexStatus   TEXT    NOT NULL DEFAULT 'Success',
                FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                UNIQUE(ProjectId, Path)
            );
            CREATE INDEX IF NOT EXISTS idx_files_indexstatus ON Files(IndexStatus);
            CREATE INDEX IF NOT EXISTS idx_files_path ON Files(Path);

            -- Symbols
            CREATE TABLE IF NOT EXISTS Symbols (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                FileId      INTEGER NOT NULL,
                Name        TEXT    NOT NULL,
                Kind        TEXT    NOT NULL,
                FullName    TEXT    NOT NULL,
                ReturnType  TEXT,
                LineStart   INTEGER NOT NULL,
                LineEnd     INTEGER NOT NULL,
                Modifiers   TEXT,
                Attributes  TEXT,
                XmlDoc      TEXT,
                IsDynamicEntrypoint INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_symbols_name      ON Symbols(Name);
            CREATE INDEX IF NOT EXISTS idx_symbols_fullname  ON Symbols(FullName);
            CREATE INDEX IF NOT EXISTS idx_symbols_kind      ON Symbols(Kind);
            CREATE INDEX IF NOT EXISTS idx_symbols_file      ON Symbols(FileId);

            -- Parameters
            CREATE TABLE IF NOT EXISTS Parameters (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                SymbolId    INTEGER NOT NULL,
                Name        TEXT    NOT NULL,
                Type        TEXT    NOT NULL,
                IsOptional  INTEGER NOT NULL DEFAULT 0,
                Position    INTEGER NOT NULL,
                FOREIGN KEY (SymbolId) REFERENCES Symbols(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_parameters_symbol ON Parameters(SymbolId);

            -- Dependencies
            CREATE TABLE IF NOT EXISTS Dependencies (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceId      INTEGER NOT NULL,
                TargetName    TEXT    NOT NULL,
                TargetId      INTEGER,
                Relationship  TEXT    NOT NULL,
                FilePath      TEXT    NOT NULL,
                Line          INTEGER NOT NULL,
                FOREIGN KEY (SourceId) REFERENCES Symbols(Id) ON DELETE CASCADE,
                FOREIGN KEY (TargetId) REFERENCES Symbols(Id) ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS idx_dependencies_source     ON Dependencies(SourceId);
            CREATE INDEX IF NOT EXISTS idx_dependencies_target     ON Dependencies(TargetName);
            CREATE INDEX IF NOT EXISTS idx_dependencies_rel        ON Dependencies(Relationship);
            CREATE INDEX IF NOT EXISTS idx_dependencies_target_id  ON Dependencies(TargetId);

            -- Views
            CREATE VIEW IF NOT EXISTS vw_symbol_hierarchy AS
            SELECT
                s.Id AS SymbolId,
                s.Name,
                s.FullName,
                d.Relationship,
                d.TargetName,
                d.TargetId
            FROM Symbols s
            INNER JOIN Dependencies d ON d.SourceId = s.Id
            WHERE d.Relationship IN ('Inherits', 'Implements');

            CREATE VIEW IF NOT EXISTS vw_symbol_dependencies AS
            SELECT
                d.Id,
                d.SourceId,
                s.FullName AS SourceFullName,
                d.TargetName,
                d.TargetId,
                d.Relationship,
                d.FilePath,
                d.Line
            FROM Dependencies d
            INNER JOIN Symbols s ON s.Id = d.SourceId;

            -- IndexRuns
            CREATE TABLE IF NOT EXISTS IndexRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT NOT NULL,
                DurationMs INTEGER NOT NULL,
                FilesProcessed INTEGER NOT NULL,
                FilesSkipped INTEGER NOT NULL,
                Status TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_indexruns_started ON IndexRuns(StartedAt);
        ";
}
