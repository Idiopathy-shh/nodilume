namespace Nodilume.Infrastructure.Sqlite;

internal static class SqliteSchema
{
    public const int CurrentVersion = 3;

    public const string MigrationV1 = """
CREATE TABLE IF NOT EXISTS schema_info (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS maps (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    revision INTEGER NOT NULL CHECK (revision >= 0),
    schema_version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS ideas (
    id TEXT PRIMARY KEY,
    map_id TEXT NOT NULL,
    title TEXT NOT NULL,
    content TEXT NOT NULL,
    UNIQUE(id, map_id),
    FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS placements (
    id TEXT PRIMARY KEY,
    map_id TEXT NOT NULL,
    idea_id TEXT NOT NULL,
    parent_id TEXT NULL,
    x REAL NOT NULL,
    y REAL NOT NULL,
    z REAL NOT NULL,
    is_pinned INTEGER NOT NULL CHECK (is_pinned IN (0, 1)),
    annotation TEXT NOT NULL,
    UNIQUE(id, map_id),
    FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE,
    FOREIGN KEY(idea_id, map_id) REFERENCES ideas(id, map_id) ON DELETE RESTRICT,
    FOREIGN KEY(parent_id, map_id) REFERENCES placements(id, map_id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS relations (
    id TEXT PRIMARY KEY,
    map_id TEXT NOT NULL,
    source_idea_id TEXT NOT NULL,
    target_idea_id TEXT NOT NULL,
    kind TEXT NOT NULL,
    is_directed INTEGER NOT NULL CHECK (is_directed IN (0, 1)),
    explanation TEXT NOT NULL,
    UNIQUE(id, map_id),
    FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE,
    FOREIGN KEY(source_idea_id, map_id) REFERENCES ideas(id, map_id) ON DELETE RESTRICT,
    FOREIGN KEY(target_idea_id, map_id) REFERENCES ideas(id, map_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_placements_parent
    ON placements(map_id, parent_id, id);
CREATE INDEX IF NOT EXISTS ix_placements_idea
    ON placements(map_id, idea_id, id);
CREATE INDEX IF NOT EXISTS ix_ideas_title
    ON ideas(map_id, title, id);
CREATE INDEX IF NOT EXISTS ix_relations_source
    ON relations(map_id, source_idea_id, id);
CREATE INDEX IF NOT EXISTS ix_relations_target
    ON relations(map_id, target_idea_id, id);
""";

    public const string MigrationV2 = """
CREATE INDEX IF NOT EXISTS ix_relations_map_id
    ON relations(map_id, id);
""";
    public const string MigrationV3 = """
CREATE TABLE IF NOT EXISTS graph_edits (
 seq INTEGER PRIMARY KEY AUTOINCREMENT,
 map_id TEXT NOT NULL, placement_id TEXT NOT NULL,
 before_x REAL NOT NULL, before_y REAL NOT NULL, before_z REAL NOT NULL, before_pin INTEGER NOT NULL,
 after_x REAL NOT NULL, after_y REAL NOT NULL, after_z REAL NOT NULL, after_pin INTEGER NOT NULL,
 FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_graph_edits_map_seq ON graph_edits(map_id, seq);
CREATE TABLE IF NOT EXISTS edit_cursor (
 map_id TEXT PRIMARY KEY, seq INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS edit_receipts (
 map_id TEXT NOT NULL, request_id TEXT NOT NULL, fingerprint TEXT NOT NULL,
 revision INTEGER NOT NULL, PRIMARY KEY(map_id, request_id),
 FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS view_state (
 map_id TEXT PRIMARY KEY, payload TEXT NOT NULL,
 FOREIGN KEY(map_id) REFERENCES maps(id) ON DELETE CASCADE
);
""";

}