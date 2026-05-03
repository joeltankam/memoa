---
name: gh-cli
description: >-
  GitHub CLI (gh) reference for repositories, issues, pull requests, Actions,
  releases, and common GitHub operations from the command line.
  For detailed flags on any command, run: gh <command> --help
---

# GitHub CLI (gh)

Reference for GitHub CLI â€” work with GitHub from the command line.
For full flag details on any command, run `gh <command> <subcommand> --help`.

## Installation and Authentication

```bash
# Install
# Follow instructions at https://github.com/cli/cli#installation
# For Windows, use the MSI installer or winget: winget install --id GitHub.cli
winget install --id GitHub.cli

# Login
gh auth login
gh auth login --hostname enterprise.internal
gh auth login --with-token < token.txt

# Status and token
gh auth status
gh auth token
```

## Key Environment Variables

| Variable | Purpose |
|----------|---------|
| `GH_TOKEN` | Auth token for automation |
| `GH_HOST` | GitHub hostname |
| `GH_REPO` | Override default repository (owner/repo) |
| `GH_PROMPT_DISABLED` | Disable interactive prompts |

## Pull Requests (gh pr)

Most `gh pr` subcommands accept `[<number> | <url> | <branch>]` as identifier.

```bash
# Create
gh pr create --title "Title" --body "Description" --base main --draft
gh pr create --body-file .github/PULL_REQUEST_TEMPLATE.md

# List and filter
gh pr list --state open --author @me --labels bug
gh pr list --json number,title,state,author,headRefName

# View
gh pr view 123
gh pr view 123 --json title,body,state,commits,files,reviews,comments
gh pr view 123 --comments

# Diff
gh pr diff 123
gh pr diff 123 --name-only

# Checkout
gh pr checkout 123

# Review
gh pr review 123 --comment --body "Review comment"
gh pr review 123 --comment --body-file review.md
gh pr review 123 --approve --body "LGTM!"
gh pr review 123 --request-changes --body "Please fix"

# Merge
gh pr merge 123 --squash --delete-branch
gh pr merge 123 --merge
gh pr merge 123 --rebase

# Edit
gh pr edit 123 --title "New title" --add-label bug --add-reviewer user1

# Other
gh pr checks 123 --watch
gh pr close 123 --comment "Closing because..."
gh pr ready 123
gh pr reopen 123
```

## Issues (gh issue)

```bash
# Create
gh issue create --title "Bug: ..." --body "Description" --labels bug --assignee @me

# List
gh issue list --state open --assignee @me --labels enhancement
gh issue list --json number,title,state,labels

# View and comment
gh issue view 456
gh issue comment 456 --body "Comment text"

# Edit and close
gh issue edit 456 --add-label priority --title "Updated"
gh issue close 456 --reason completed
```

## Repositories (gh repo)

```bash
# Clone and view
gh repo clone owner/repo
gh repo view owner/repo
gh repo view --json name,description,defaultBranchRef

# Create
gh repo create my-repo --public --clone
gh repo create my-repo --private --template owner/template

# Sync fork
gh repo sync --branch main
```

## GitHub Actions (gh run / gh workflow)

```bash
# List and view runs
gh run list --workflow "ci.yml" --branch main --limit 20
gh run view 123456789
gh run view 123456789 --log

# Watch, rerun, cancel
gh run watch 123456789
gh run rerun 123456789
gh run cancel 123456789

# Download artifacts
gh run download 123456789 --name build --dir ./artifacts

# Workflows
gh workflow list
gh workflow run ci.yml --ref develop
gh workflow enable ci.yml
gh workflow disable ci.yml
```

## Secrets and Variables

```bash
# Secrets
gh secret list
gh secret set MY_SECRET
gh secret set MY_SECRET --env production
gh secret delete MY_SECRET

# Variables
gh variable list
gh variable set MY_VAR "value"
gh variable get MY_VAR
gh variable delete MY_VAR
```

## Releases (gh release)

```bash
gh release create v1.0.0 --title "v1.0.0" --notes "Release notes"
gh release create v1.0.0 --generate-notes ./build/*.zip
gh release list
gh release view v1.0.0
gh release download v1.0.0 --dir ./releases
```

## API Requests (gh api)

```bash
# GET request
gh api repos/{owner}/{repo}/pulls

# POST request
gh api repos/{owner}/{repo}/issues -f title="Bug" -f body="Details"

# With pagination
gh api repos/{owner}/{repo}/pulls --paginate

# GraphQL
gh api graphql -f query='{ viewer { login } }'

# JSON output with jq
gh api repos/{owner}/{repo}/pulls --jq '.[].title'
```

## Search (gh search)

```bash
gh search issues "bug" --repo owner/repo --state open
gh search prs "feature" --author @me
gh search code "function_name" --repo owner/repo
gh search repos "topic" --language go
```

## Other Commands

| Command | Purpose |
|---------|---------|
| `gh browse` | Open repo/file/PR in browser |
| `gh browse 123` | Open issue/PR #123 in browser |
| `gh gist create file.txt` | Create a gist |
| `gh label list` | List repository labels |
| `gh project list` | List projects |
| `gh config set editor vim` | Set configuration |
| `gh alias set co 'pr checkout'` | Create command alias |
| `gh extension install owner/ext` | Install extension |
