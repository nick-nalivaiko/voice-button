# Security Policy

## Reporting a vulnerability

Do not post live API keys, access tokens, credentials, or private diagnostic logs in a public issue.

Use GitHub's private security advisory flow for vulnerabilities. If a credential may have been exposed, revoke or rotate it with the provider immediately; deleting it from the latest commit is not sufficient because Git history remains available.

## Secret handling

Voice Button stores OpenAI API keys in Windows Credential Manager. Local environment files, settings, diagnostics, build output, release packages, archives, and private-key containers are excluded from Git.

Commits are checked by the repository pre-commit hook and GitHub Actions. Release packaging also runs the full-history secret scan before producing public files.
