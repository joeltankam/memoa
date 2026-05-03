---
description: 'PowerShell scripting guidelines'
applyTo: '**/*.ps1,**/*.psm1'
---

# PowerShell Development Guidelines

## Safety and Initialization

Every script must begin with these essential directives:

```powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
```

- `Set-StrictMode -Version Latest` enforces strict variable declaration and reference semantics.
- `$ErrorActionPreference = "Stop"` terminates on any error—prevents silent failures.
- These must appear before any other code in the script.

## Compatibility and Environment

- All scripts must be compatible with **PowerShell 5.1** (Windows PowerShell and PowerShell Core).
- Use `.ps1` extension for scripts and `.psm1` for modules.
- Avoid PowerShell 7+ specific features unless targeting modern PowerShell exclusively.

## Naming Conventions

- Use **Verb-Noun format** for function names: `Get-Configuration`, `Start-Process`, `Stop-Service`.
- Use **approved PowerShell verbs** (run `Get-Verb` to list).
- Use **PascalCase** for function names, parameters, and variables: `$MyVariable`, `$ProcessId`.
- Use **lowercase for private/internal** variables when prefixed with `$private:`.
- Avoid abbreviations; use clear, descriptive names.

## Code Formatting

- Use **4 spaces** for indentation (per `.editorconfig`).
- Place opening braces on the same line as statements: `if ($condition) {` (not on new line).
- Ensure exactly **one newline at end of file**.
- Use CRLF line endings for `.ps1` and `.psm1` files on Windows.
- Trim trailing whitespace on all lines.

## Functions and Parameters

- Use `[CmdletBinding()]` for advanced functions with parameters.
- Declare parameters with type validation: `[Parameter(Mandatory = $true)][string]$Name`.
- Use common parameter names following PowerShell conventions: `Path`, `Name`, `Force`, `Verbose`.
- Implement `Begin`, `Process`, and `End` blocks for functions accepting pipeline input.

## Error Handling

- Use `try`/`catch` for exception handling; avoid silent failures.
- Throw terminating errors with `throw` or `$PSCmdlet.ThrowTerminatingError()` in advanced functions.
- Use `Write-Error` for non-terminating errors only when explicitly needed.
- Validate input at entry points; fail fast on invalid parameters.
- Use `SupportsShouldProcess = $true` for destructive operations.

## Output and Logging

- Return **objects**, not formatted strings.
- Use `Write-Verbose` for diagnostic information with `-Verbose` flag.
- Use `Write-Warning` for warning conditions (non-fatal).
- Avoid `Write-Host` except for user-facing prompts.
- Return structured output using `[PSCustomObject]` for complex data.

## Scripting Best Practices

- Avoid aliases in scripts; use full cmdlet names: `Get-ChildItem` not `gci`.
- Always use full parameter names.
- Avoid `Read-Host` for interactive input in automated scripts; use parameters instead.
- Use `$null` comparison for null checks: `if ($variable -eq $null)`.
- Prefer `Join-Path` for path concatenation over string concatenation.
- Document functions with PowerShell comment-based help:

```powershell
<#
.SYNOPSIS
    Brief description

.DESCRIPTION
    Detailed explanation

.PARAMETER Name
    Parameter description

.EXAMPLE
    Get-Configuration -Name 'prod'

.OUTPUTS
    [PSCustomObject]
#>
```

## Testing and Documentation

- Write test cases for critical paths and error scenarios.
- Provide examples in help documentation.
- Log significant operations with timestamps for debugging.
- Include comments for non-obvious logic.
