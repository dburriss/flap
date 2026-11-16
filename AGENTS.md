# AGENTS.md

Flap: a clap-style, pipe-friendly CLI parser for F#. No attributes, no reflection; must stay Native AOT and Fable compatible.

## Layout
- `src/Flap` — library (targets `net10.0` and `netstandard2.1`). F# compile order matters: `Types → Arg → Matches → Help → Parser → Completions → Command` (see `Flap.fsproj`).
- `tests/Flap.Tests` — xUnit v3 tests (Microsoft.Testing.Platform).
- `samples/Flap.Sample` (Native AOT), `samples/Flap.Fable` (Fable).

## Commands
- Test: `dotnet test tests/Flap.Tests`
- AOT sample: `dotnet publish samples/Flap.Sample -c Release -r osx-arm64`
- Fable sample: `cd samples/Flap.Fable && dotnet fable --lang js`

## Rules
- Library is built with `--reflectionfree` and `TreatWarningsAsErrors`; don't use `%A`/`sprintf "%A"`, reflection, or APIs unavailable in Fable/netstandard2.1.
- Keep the parsing core pure (`Command.tryParse*` returns `Result`); console I/O belongs only in `Command.parse`.
- Add tests for behaviour changes; update `README.md` when the public API changes.

## Commits & changelog
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, ...; `!` or `BREAKING CHANGE:` for breaking changes).
- Record every user- or developer-facing change in `CHANGELOG.md` under `## [Unreleased]`, following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) (Added / Changed / Deprecated / Removed / Fixed / Security).
