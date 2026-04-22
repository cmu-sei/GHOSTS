# Contributing to GHOSTS

Thank you for your interest in contributing to GHOSTS. This guide covers how to report issues, submit changes, and set up a development environment.

## Reporting Issues

Use the [GitHub issue tracker](https://github.com/cmu-sei/GHOSTS/issues) for bugs and feature requests. Include:

- **Component**: API, Frontend, Windows Client, Universal Client, Lite, Pandora, Animator, Docs
- **Version**: GHOSTS version and OS/runtime details
- **Steps to reproduce**: Minimal steps to trigger the issue
- **Expected vs. actual behavior**
- **Logs**: Relevant excerpts from `logs/app.log` or browser console

For security vulnerabilities, see [SECURITY.md](SECURITY.md) instead.

## Pull Requests

### Branch Naming

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feature/<short-description>` | `feature/ssh-handler-timeout` |
| Bug fix | `fix/<short-description>` | `fix/timeline-cron-parse` |
| Documentation | `docs/<short-description>` | `docs/client-troubleshooting` |
| Refactor | `refactor/<short-description>` | `refactor/handler-base-class` |

### PR Process

1. Fork the repository and create your branch from `master`.
2. Make your changes. Keep PRs focused — one concern per PR.
3. Add or update tests if applicable.
4. Ensure the project builds cleanly (`dotnet build` for .NET, `npm run build` for Frontend).
5. Submit a pull request with a clear title and description explaining **what** changed and **why**.

### PR Checklist

- [ ] Builds without errors
- [ ] No hardcoded credentials, secrets, or environment-specific paths
- [ ] New database fields include a migration (`dotnet ef migrations add <Name>`)
- [ ] Documentation updated if user-facing behavior changed
- [ ] Tested locally (API: Swagger, Frontend: browser, Client: timeline execution)

### What We Look For

- **Correctness**: Does it work? Does it handle edge cases?
- **Simplicity**: Prefer clear, direct code over clever abstractions.
- **Consistency**: Follow existing patterns in the codebase.
- **Scope**: Changes should match the PR description — no drive-by refactors.

## AI-Assisted Contributions

We welcome contributions that use AI tools (Claude Code, Codex, etc.). AI can be great for targeted fixes, test generation, documentation, and well-scoped features. That said, a few ground rules:

**What works well:**
- Bug fixes with a clear before/after
- Adding tests for existing code
- Documentation improvements
- Small, focused features where you can explain every line in the diff

**What we'll push back on:**
- Broad rewrites or refactors that touch many files without a prior discussion. Open an issue first and get agreement on the approach before submitting a large AI-generated restructuring.
- PRs where the contributor can't explain why a specific change was made. You're responsible for understanding and defending the diff, regardless of who (or what) wrote it.
- Speculative additions — code that "might be useful" or adds abstractions, error handling, or features nobody asked for. AI tools tend toward this; trim it before submitting.

**Expectations:**
- **Disclose AI use** in your PR description. A simple "Used Claude Code for X" or "Copilot-assisted" is fine. No need to justify the tooling, just be transparent.
- **Review the output yourself** before submitting. Build it, test it, read the diff line by line. If you wouldn't be comfortable explaining a change in code review, remove it.
- **Keep the scope tight.** If an AI tool suggests fixing "one more thing" outside your PR's scope, don't include it. File a separate issue instead.

The bar for merging is the same whether code is hand-written or AI-generated: it must be correct, focused, consistent with the codebase, and something the contributor can stand behind.

## Code Style

- **.NET**: Follow existing conventions in the codebase. Use `var` when the type is obvious. Prefer async/await.
- **Angular/TypeScript**: Follow the Angular style guide. Use strict typing.
- **Commits**: Write clear, descriptive commit messages. One logical change per commit.

## Documentation

Docs live in the `docs/` directory and are built with [MkDocs](https://www.mkdocs.org/).

```bash
# Preview locally
pip install mkdocs-material
mkdocs serve
# Docs at http://localhost:8000
```

## Questions?

- [GitHub Discussions](https://github.com/cmu-sei/GHOSTS/discussions) for questions and ideas
- [GitHub Issues](https://github.com/cmu-sei/GHOSTS/issues) for bugs and feature requests

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE.md).
