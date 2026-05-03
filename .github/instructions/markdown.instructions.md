---
description: 'Markdown writing guidelines for documentation'
applyTo: '**/*.md'
---

# Markdown Writing Guidelines

## File Structure

- Every Markdown file must start with a single top-level H1 heading (`#`).
- Headings must be surrounded by blank lines above and below.
- Heading levels must increment by one (H1 → H2 → H3, no skipping).
- Use ATX-style headings (`#`, `##`, etc.), not Setext style (underlines).
- Do not include trailing punctuation in headings.
- No duplicate heading titles within the same file.

## Content Formatting

- Hard wrap text at **160 characters** per line for readability in source.
- Use **2 spaces** for indentation in YAML front matter and nested lists.
- Trim trailing whitespace on all lines.
- Ensure exactly one blank line between major sections.
- No more than one consecutive blank line anywhere in the document.

## Lists and Code

- Use dash (`-`) for unordered lists; follow with a single space.
- Maintain consistent indentation: 2 spaces per nesting level.
- Use ordered lists (`1.`, `2.`, etc.) for sequential steps; indent nested items by 2 spaces.
- Blank lines are required before and after code blocks.
- Always specify the language for fenced code blocks (e.g., ` ```csharp `).
- Do not use indented code blocks (4 spaces); use fenced code blocks instead.

## Links and Images

- Use reference-style links for URLs that appear multiple times.
- Bare URLs must be wrapped in angle brackets or link syntax.
- All images must include descriptive alternative text: `![alt text](path/to/image.png)`.
- Fragment identifiers in links must be valid (correspond to existing headings).
- Avoid empty links: `[text]()` is not allowed.

## Emphasis and Lists

- Use `*emphasis*` or `_emphasis_` consistently throughout the document (pick one style).
- Use `**strong**` or `__strong__` consistently (pick one style).
- Use backticks for inline code: ``` `variable` ``, ``` `function()` ```, ``` `ClassName` ```.
- No spaces inside emphasis or code delimiters: `` ` code ` `` is incorrect.
- Blockquote syntax: `>` followed by a single space.

## Special Considerations

- No inline HTML elements; use Markdown syntax exclusively.
- Use numbered sections or a table of contents for longer documents.
- Use horizontal rules sparingly, and format consistently: `---` on its own line with blank lines above and below.
- Escape special characters (e.g., `\*`, `\[`) when necessary.

## File Ending

- Ensure every file ends with exactly one newline character.
- Do not include trailing blank lines.

## Best Practices

- Write clear, concise prose; avoid jargon without explanation.
- Use active voice and present tense.
- Include code examples where helpful; ensure they are syntactically correct.
- Reference related documentation using relative links where possible.
