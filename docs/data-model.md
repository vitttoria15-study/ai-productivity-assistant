# Data Model

## Entities

### journal_entries

Stores raw user journal text. One entry per submission.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `entry_text` | TEXT | NOT NULL | Free-form journal text as entered by the user |
| `created_at` | TEXT | NOT NULL, DEFAULT `datetime('now')` | ISO 8601 UTC |

---

### ai_extractions

Stores the structured AI output for a journal entry. One-to-one with `journal_entries`.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `journal_entry_id` | INTEGER | NOT NULL, FK → `journal_entries.id` | One-to-one relationship |
| `completed_tasks` | TEXT | NOT NULL, DEFAULT `'[]'` | JSON array of strings |
| `blockers` | TEXT | NOT NULL, DEFAULT `'[]'` | JSON array of strings |
| `new_tasks` | TEXT | NOT NULL, DEFAULT `'[]'` | JSON array of `{title, priority}` objects |
| `summary` | TEXT | NOT NULL, DEFAULT `''` | Plain text, 1–3 sentences |
| `created_at` | TEXT | NOT NULL, DEFAULT `datetime('now')` | ISO 8601 UTC |

> Arrays stored as JSON text. SQLite's `json_each()` / `json_extract()` can query them if needed, but for MVP, deserialize in the application layer.

---

### tasks

Stores individual tasks — both AI-extracted and manually created.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `title` | TEXT | NOT NULL | Task description |
| `priority` | TEXT | NOT NULL, DEFAULT `'medium'` | `'high'`, `'medium'`, `'low'` |
| `status` | TEXT | NOT NULL, DEFAULT `'pending'` | `'pending'`, `'done'` |
| `source` | TEXT | NOT NULL, DEFAULT `'manual'` | `'ai'`, `'manual'` |
| `journal_entry_id` | INTEGER | NULL, FK → `journal_entries.id` | Non-null only when `source = 'ai'` |
| `created_at` | TEXT | NOT NULL, DEFAULT `datetime('now')` | ISO 8601 UTC |
| `updated_at` | TEXT | NOT NULL, DEFAULT `datetime('now')` | ISO 8601 UTC; update on every write |

---

### chat_messages *(optional for MVP)*

Persists chat conversation history. For the MVP, in-memory (React state) is acceptable. Include this table only if time allows in Week 2.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `role` | TEXT | NOT NULL, CHECK `role IN ('user','assistant')` | |
| `content` | TEXT | NOT NULL | Message text |
| `created_at` | TEXT | NOT NULL, DEFAULT `datetime('now')` | ISO 8601 UTC |

---

## Relationships

```
journal_entries  (1) ─────── (0..1) ai_extractions
journal_entries  (1) ─────── (0..N) tasks           [tasks.source = 'ai']
```

- A journal entry always has at most one extraction (created on first successful AI call).
- Manual tasks (`source = 'manual'`) have `journal_entry_id = NULL`.
- AI-sourced tasks have `journal_entry_id` pointing to the originating entry.

---

## SQLite Schema (DDL)

```sql
CREATE TABLE journal_entries (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    entry_text TEXT    NOT NULL,
    created_at TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE ai_extractions (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    journal_entry_id INTEGER NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    completed_tasks  TEXT    NOT NULL DEFAULT '[]',
    blockers         TEXT    NOT NULL DEFAULT '[]',
    new_tasks        TEXT    NOT NULL DEFAULT '[]',
    summary          TEXT    NOT NULL DEFAULT '',
    created_at       TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE tasks (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    title            TEXT    NOT NULL,
    priority         TEXT    NOT NULL DEFAULT 'medium'
                             CHECK(priority IN ('high', 'medium', 'low')),
    status           TEXT    NOT NULL DEFAULT 'pending'
                             CHECK(status IN ('pending', 'done')),
    source           TEXT    NOT NULL DEFAULT 'manual'
                             CHECK(source IN ('ai', 'manual')),
    journal_entry_id INTEGER REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at       TEXT    NOT NULL DEFAULT (datetime('now')),
    updated_at       TEXT    NOT NULL DEFAULT (datetime('now'))
);

-- Optional: include only if chat persistence is implemented
CREATE TABLE chat_messages (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    role       TEXT    NOT NULL CHECK(role IN ('user', 'assistant')),
    content    TEXT    NOT NULL,
    created_at TEXT    NOT NULL DEFAULT (datetime('now'))
);
```

---

## EF Core Notes

- Use `EnsureCreated()` for PoC setup — avoids migration overhead.
- Store array columns (`completed_tasks`, `blockers`, `new_tasks`) as `string` in C# entities and serialize/deserialize with `System.Text.Json` in the service layer.
- SQLite stores `TEXT` for all date columns; use `DateTime` in C# and configure EF Core to use ISO 8601 strings.
- `updated_at` must be set explicitly in the repository on every update; SQLite triggers are an alternative but add complexity.

---

## Future Extension: Vector Storage for RAG

When adding lightweight RAG, extend the schema with:

```sql
CREATE TABLE journal_embeddings (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    journal_entry_id INTEGER NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    chunk_text       TEXT    NOT NULL,
    embedding        BLOB    NOT NULL,   -- float32 array serialized as bytes
    created_at       TEXT    NOT NULL DEFAULT (datetime('now'))
);
```

Or replace with an embedded vector store (Chroma, sqlite-vss). No changes to `journal_entries`, `tasks`, or `ai_extractions` are required.
