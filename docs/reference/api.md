# API reference

All builders take the thing being built as their last argument, so they pipe: `Arg.create "x" |> Arg.long "x"`.

## `Command`

| Function | Purpose |
|---|---|
| `create name` | New command |
| `version v` | Version text (enables `--version`) |
| `about a` | Short description |
| `longAbout a` | Description shown for `--help` only |
| `beforeHelp t` / `afterHelp t` | Text above / below the help sections |
| `author a` | Author text |
| `arg a` / `args xs` | Add argument(s) |
| `subcommand c` / `subcommands cs` | Add subcommand(s) |
| `alias a` | Alternate command name |
| `group id members required multiple` | Define an argument group |
| `subcommandRequired` | Error when no subcommand given |
| `argRequiredElseHelp` | Show help when no args given |
| `allowExternalSubcommands` | Collect unknown subcommands (see `Matches.externalArgs`) |
| `disableHelpFlag` / `disableVersionFlag` | Remove the built-in flags |
| `hidden` | Hide from help and completions |
| `check cmd` | List definition errors (`string list`) |
| `debugAssert cmd` | Throw on definition errors (use in tests/startup) |

### Parsing

| Function | Returns | Notes |
|---|---|---|
| `tryParse argv cmd` | `Result<Matches, ParseError>` | Pure; ignores environment variables |
| `tryParseWith env argv cmd` | `Result<Matches, ParseError>` | Pure; `env` resolves `Arg.env` |
| `parseWith env out err exit argv cmd` | `Matches` | Callbacks for output and exit |
| `parse argv cmd` | `Matches` | Uses console, prints and exits. Not available under Fable |

## `Arg`

| Function | Purpose |
|---|---|
| `create id` | New argument |
| `short c` / `long l` / `alias l` | Option spellings (`-c`, `--l`) |
| `help h` / `valueName n` | Help text / placeholder name |
| `helpHeading h` | Group under a custom help section |
| `hidden` | Hide from help and completions |
| `global'` | Propagate to all subcommands |
| `positional i` | Positional at 1-based index |
| `required` | Must be supplied |
| `defaultValue v` | Value when absent |
| `env name` | Read from environment variable when absent |
| `delimiter c` | Split a value on `c` |
| `possibleValues vs` | Restrict (and complete) to these values |
| `validate f` | Custom `string -> Result<unit, string>` |
| `requires id` / `conflictsWith id` / `requiredUnless id` | Relationships to other args |
| `group g` | Add to argument group `g` |
| `valueHint h` | Hint for path completion |
| `allowHyphenValues` | Value may start with `-` |
| `trailingVarArg` | Last positional takes everything after its first value verbatim |

### Argument kinds

| Function | Action | Values |
|---|---|---|
| `flag` | `SetTrue` | 0 |
| `count` | `Count` (`-vvv`) | 0 |
| `append` | `Append`, repeatable option | 1 per occurrence |
| `manyValues` | `Append` | 1 to unlimited |
| `numArgs min max` | | `min` to `max` (per occurrence for options, total for positionals) |
| `action a` | Set `ArgAction` directly | |

`ArgAction`: `Set`, `Append`, `SetTrue`, `SetFalse`, `Count`, `Help`, `Version`.
`ValueHint`: `AnyPath`, `FilePath`, `DirPath`, `Other`.

## `Matches`

| Function | Returns |
|---|---|
| `contains id` | `bool` |
| `tryString id` | `string option` |
| `getString id` | `string` (throws if absent) |
| `getMany id` | `string list` |
| `flag id` | `bool` |
| `count id` | `int` |
| `tryInt` / `tryBool` / `tryFloat id` | `Result<'T option, string>`: `Ok None` if absent, `Error` if unparseable |
| `getInt` / `getBool` / `getFloat id` | `Result<'T, string>` (required) |
| `tryGetWith parse id` / `getWith parse id` / `getManyWith parse id` | Same, with your own parser |
| `valueSource id` | `ValueSource option`: `DefaultValue`, `EnvVar`, `CommandLine` |
| `subcommand` | `(string * Matches) option` |
| `trySubcommand name` | `Matches option` |
| `externalArgs` | Tokens given to an external subcommand |

## `Value` parsers

`Value.string`, `Value.int`, `Value.int64`, `Value.float`, `Value.bool` (`true/yes/1/on`, `false/no/0/off`),
and `Value.oneOf [ "a", A; "b", B ]` for mapping strings to cases. Each is `string -> Result<'T, string>`.

## `ParseError`

| Case | Meaning |
|---|---|
| `DisplayHelp` / `DisplayVersion` | Help or version requested (exit code 0) |
| `MissingRequired`, `MissingValue`, `MissingSubcommand` | Something required is absent |
| `UnknownArgument`, `UnknownSubcommand` | Unrecognised input, with an optional suggestion |
| `InvalidValue`, `TooManyValues`, `TooFewValues` | Bad values |
| `ArgumentConflict`, `MissingRequiredBy` | `conflictsWith` / `requires` violations |
| `GroupConflict`, `GroupRequired` | Group violations |
| `DefinitionError` | The command definition is invalid |

`ParseError.render e` gives the user-facing message. `ParseError.exitCode e` gives `0` for help/version and `2` otherwise.
