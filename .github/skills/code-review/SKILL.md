---
name: code-review
description: >-
  Lightweight code review guidance focused on correctness, quality, and
  maintainability. Use when reviewing pull requests or local diffs.
---

# Code Review Skill

Use this skill to evaluate code changes with high signal and low noise.

## When To Use

- Reviewing a pull request.
- Reviewing local changes before opening a pull request.
- Giving feedback on design tradeoffs or implementation risks.

## Review Principles

1. Form an independent understanding from code and diff first.
2. Prioritize issues that can cause wrong behavior, outages, or costly maintenance.
3. Avoid speculative comments and style-only nitpicks.
4. Be specific, actionable, and concise.

## What To Check

- Correctness: logic errors, edge cases, null handling, failure paths.
- Reliability: retries, timeouts, cancellation, resource cleanup, race conditions.
- Security: input validation, auth boundaries, secret handling, unsafe assumptions.
- Performance: obvious regressions, repeated expensive work, unbounded growth.
- Maintainability: unnecessary complexity, duplication, unclear ownership, brittle abstractions.
- Compatibility: breaking behavior or API changes and migration impact.
- Tests: coverage of happy path, edge cases, and newly introduced failure modes.

## Feedback Quality Bar

For each issue, include:

- What is wrong.
- Why it matters.
- What to change.

If confidence is low, phrase feedback as a question and state the uncertainty.

## Suggested Output Structure

1. Overall assessment (1-3 sentences).
2. Blocking issues (if any).
3. Important non-blocking risks.
4. Testing gaps.
5. Optional follow-ups.

## Severity Guidance

- `error`: Must fix before merge.
- `warning`: Should fix soon; could become a defect.
- `suggestion`: Nice improvement, not merge-blocking.
