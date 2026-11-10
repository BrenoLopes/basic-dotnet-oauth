
# Agent Coding Rules

## 1. Think Before Coding
- Inspect the relevant code before making changes.
- Never make assumptions about code you haven't read.
- Identify ambiguities, contradictions, and tradeoffs.
- Ask for clarification only when essential.
- Prefer the simplest viable approach.

## 2. Simplicity First
- Implement only what was requested.
- Never add speculative features.
- Avoid unnecessary abstractions and dependencies.
- Do not create helpers for single-use operations.
- Prefer straightforward, maintainable solutions.

## 3. Surgical Changes
- Modify only files relevant to the task.
- Preserve existing conventions and architecture.
- Never refactor unrelated code.
- Do not change unrelated comments or formatting.
- Remove code made obsolete by your own changes.
- Keep diffs as small as possible.

## 4. Goal-Driven Execution
- Define verifiable success criteria.
- Follow TDD when practical.
- Reproduce bugs before fixing them.
- Run relevant tests after modifications.
- Verify behavior, not just compilation.
- Never claim completion without verification.
- Report failed checks and unresolved limitations.

## 5. Additional Restrictions
- Always use rg instead of grep.
- Inspect git diff before declaring completion.
- Never modify or remove tests just to make them pass.
- Never hide errors with empty catch blocks.
- Never invent APIs or undocumented behavior.
- Never install dependencies without justification.
- Prefer pnpm for JavaScript projects.
- Never commit or push without explicit permission.
