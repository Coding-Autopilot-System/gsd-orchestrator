# Contributing

Contributions are welcome when they improve the Coding Autopilot System portfolio
without weakening its safety boundaries.

## Before You Start

1. Search existing issues and pull requests.
2. Open an issue before making a cross-repository, architectural, or breaking change.
3. Never include credentials, personal data, private prompts, or proprietary source.
4. Keep autonomous changes bounded, reviewable, and reversible.

## Development Workflow

1. Fork or branch from the repository's default branch.
2. Make the smallest coherent change.
3. Add or update tests and documentation.
4. Run the repository's documented validation commands.
5. Open a pull request using the shared template.

Use conventional commit subjects where practical, for example:

```text
feat(scope): add bounded capability
fix(scope): handle failed validation
docs(scope): clarify operator runbook
```

## Pull Request Standard

A pull request must explain intent, risk, verification, and rollback. Changes to
workflows, identities, permissions, dependencies, or autonomous behavior require
explicit reviewer attention.

Maintainers may close changes that are unsafe, untested, unrelated to the repository,
or generated without meaningful human review.

## Responsible AI Expectations

- Treat model output as untrusted input.
- Require human approval before consequential or destructive actions.
- Apply least privilege to tools, identities, and repository permissions.
- Preserve audit evidence for autonomous operations.
- Document known limitations and failure modes.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
