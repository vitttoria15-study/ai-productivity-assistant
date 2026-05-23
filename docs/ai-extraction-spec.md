# AI Extraction Spec

## Input

Plain-text journal entry.

Example:

```text
Finished API integration today.
Need to fix frontend validation tomorrow.
Blocked by missing environment variables.
```

## Output

Return valid JSON only.

```json
{
  "completed_tasks": [],
  "new_tasks": [],
  "blockers": [],
  "priorities": [],
  "summary": ""
}
```

## Rules

* No markdown.
* No explanations.
* Always return arrays.
* Keep summary short.
* If nothing found → return empty arrays.

---

## Output Destinations

After the AI returns structured JSON, the backend routes each field:

| Field | Destination |
| --- | --- |
| `new_tasks` | Pushed to Todoist API — created as new Todoist tasks |
| `completed_tasks` | Todoist API — matching tasks marked as done |
| `blockers` | Saved to SQLite as `ExtractedBlocker` records linked to the journal entry |
| `summary` | Saved to SQLite as `JournalEntry.Summary` |
| `priorities` | Used in prompt context for chat; not persisted separately |

Tasks are never written to SQLite.

---

