# AGENTS.md

Flap: a clap-style, pipe-friendly CLI parser for F#. No attributes, no reflection; must stay Native AOT and Fable compatible.

## Layout
- `src/Flap` — library (targets `net10.0` and `netstandard2.1`). F# compile order matters: `Types → Arg → Matches → Help → Parser → Completions → Command` (see `Flap.fsproj`).
- `tests/Flap.Tests` — xUnit v3 tests (Microsoft.Testing.Platform).
- `samples/` — example projects.

## Commands
- Build: `dotnet build flap.slnx`
- Test: `dotnet test tests/Flap.Tests`
- Build and run the tests **before** starting a change (to know the baseline) and **again after** it. Report the results of both; don't claim a change works from a build alone.
- AOT sample: `dotnet publish samples/Flap.Sample -c Release -r osx-arm64`
- Fable sample: `cd samples/Flap.Fable && dotnet fable --lang js`

## Rules
- Library is built with `--reflectionfree` and `TreatWarningsAsErrors`; don't use `%A`/`sprintf "%A"`, reflection, or APIs unavailable in Fable/netstandard2.1.
- Keep the parsing core pure (`Command.tryParse*` returns `Result`); console I/O belongs only in `Command.parse`.
- Add tests for behaviour changes; update `README.md` and the matching page in `docs/` (Diataxis layout, see `docs/index.md`) when the public API changes.

## Branching
- Work directly on `main`. Don't create branches or worktrees without asking first.
- If a change seems experimental or risky, ask permission before branching.

## Releasing
- Run `./publish.sh` (`--dry-run` first): it bumps `<Version>` in `src/Flap/Flap.fsproj`, rolls `CHANGELOG.md`, commits and tags `vX.Y.Z`. Pushing the tag triggers `.github/workflows/publish-nuget.yml`, which publishes to NuGet via Trusted Publishing (needs the `NUGET_USER` secret and a nuget.org policy naming `publish-nuget.yml`).
- CI takes the version from the tag; don't hand-edit `<Version>` or `<PackageReleaseNotes>`.

## Commits & changelog
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, ...; `!` or `BREAKING CHANGE:` for breaking changes).
- Record every user- or developer-facing change in `CHANGELOG.md` under `## [Unreleased]`, following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) (Added / Changed / Deprecated / Removed / Fixed / Security).
- Don't log documentation-only changes (README, `AGENTS.md`, `CLAUDE.md`, docs, comments) in the changelog.
