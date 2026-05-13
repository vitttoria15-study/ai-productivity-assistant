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

