# Completions reference

```fsharp
Completions.generate : Shell -> Command -> string
```

`Shell` is one of `Bash`, `Zsh`, `Fish`, `PowerShell`, `Nushell`. The function is pure and returns the script text.

## What is completed

| Item | Source |
|---|---|
| Subcommands (and aliases) | `Command.subcommand`, `Command.alias` |
| Options and flags (`-s`, `--long`, aliases) | `Arg` definitions, plus inherited global args |
| Option values | `Arg.possibleValues` after `--opt <TAB>` and `--opt=<TAB>` |
| Positional values | `Arg.possibleValues` at the positional's slot |
| Paths | `Arg.valueHint` |

Hidden arguments and hidden subcommands are omitted. Values are scoped to the subcommand (and inherited global args) they belong to.

## `Arg.valueHint`

| Hint | Completes |
|---|---|
| `AnyPath` | Files and directories |
| `FilePath` | Files |
| `DirPath` | Directories |
| `Other` or unset | No path completion |

Each shell uses its native mechanism for path completion.

## Per-shell notes

| Shell | Notes |
|---|---|
| Fish | Multi-line help text is collapsed to one line |
| Nushell | Emits an `export extern` per command level (and alias), `def "nu-complete ..."` completers for possible values, and `path` / `directory` types for value hints |
