---
name: Always use Edit/Write tools for file edits
description: Never fall back to Bash for file writes — always use the dedicated Edit or Write tools
type: feedback
---

Never use Bash (cat, echo, heredoc, etc.) to write or edit files. Always use the Edit tool for modifications and Write tool for new files.

**Why:** User finds it unacceptable and prefers the dedicated tools for clarity and review.

**How to apply:** Even when Edit fails due to stale reads, re-read the file first then use Edit again — do not reach for Bash as a workaround.
