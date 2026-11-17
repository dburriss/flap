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
| Runtime values | `Arg.completer`, `Arg.completerValues` |

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

## Runtime completion

### Types and functions

```fsharp
type Candidate = { Value: string; Help: string option }

Arg.completer       : (string -> Candidate list) -> Arg -> Arg
Arg.completerValues : (string -> string list)    -> Arg -> Arg

Complete.candidates : string list -> Command -> Candidate list
Complete.envVarName : Command -> string
Complete.render     : Shell -> Candidate list -> string
Complete.respond    : (string -> string option) -> string list -> Command -> string option
Complete.tryParseShell : string -> Shell option
```

All of these are pure. `Complete.candidates` takes the words after the program name; the last element is the word being completed (`""` when the cursor follows a space). It never fails on partial or unknown input.

### What the completer receives

The completer gets the partial word for the value being completed (for `--opt=par`, just `par`). Its result is returned as is, not filtered. Static `Arg.possibleValues` are filtered by prefix. If an arg has both, the completer is used.

### Protocol

A generated script calls the program with:

- the environment variable `<NAME>_COMPLETE` set to `bash`, `zsh`, `fish`, `powershell` or `nushell`, and
- the words after the program name up to and including the word being completed.

`<NAME>` is the command name upper-cased with every non-alphanumeric character replaced by `_` (`my-tool` becomes `MY_TOOL_COMPLETE`). Names that differ only in such characters (`my-tool`, `my_tool`) share a variable.

`Command.parse` and `Command.parseWith` print the candidates and exit with code 0 when the variable names a known shell. `Command.tryParse` is unaffected; use `Complete.respond` before it.

The shell name is matched case-insensitively. `pwsh` and `nu` are accepted as aliases. An unrecognised value is ignored and normal parsing continues.

### Output format

| Shell | Output, one candidate per line |
|---|---|
| Bash | `value` |
| Zsh, Fish, PowerShell, Nushell | `value<TAB>description` (the description may be empty) |

Tabs and newlines inside a value or description are replaced by spaces.

### Which scripts call back

A script calls the program only for options and positionals that have a completer. Static values, flags, subcommands and paths are completed by the script itself. If no arg in the command tree has a completer, the script contains no call-back code.

### Per-shell notes

| Shell | Call-back |
|---|---|
| Bash | Runs the program with `COMP_WORDS`; handles the `--opt` `=` `value` split |
| Zsh | Runs the program with `words`; shows descriptions with `compadd -d` |
| Fish | A `__<name>_flap_dyn` helper added as the completion source; Fish reads the descriptions itself |
| PowerShell | Sets the variable for the call and removes it afterwards |
| Nushell | A `nu-complete ...` completer taking `[context: string]`; reads the output with `from tsv --no-infer` so values stay strings |
