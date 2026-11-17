namespace Flap

module Completions =

    open CompletionModel

    // ---------------------------------------------------------------------------------------------
    // Quoting
    // ---------------------------------------------------------------------------------------------

    /// POSIX single-quoted word (bash/zsh).
    let private sq (s: string) = "'" + s.Replace("'", "'\\''") + "'"

    /// PowerShell single-quoted string.
    let private psq (s: string) = "'" + s.Replace("'", "''") + "'"

    /// fish single-quoted string.
    let private fsq (s: string) = "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'"

    /// Collapse help text to a single line (shells treat each output line as a separate candidate).
    let private oneLine (s: string) = s.Replace("\r", "").Replace("\n", " ").Trim()

    /// fish double-quoted string.
    let private fdq (s: string) =
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$") + "\""

    // ---------------------------------------------------------------------------------------------
    // bash / zsh (shared structure: walk words, track subcommand context, positional count and
    // whether the word being completed is the value of an option)
    // ---------------------------------------------------------------------------------------------

    let private posCond (var: string) (s: Slot) =
        match s.Last with
        | Some l when l = s.First -> "(( " + var + " == " + string s.First + " ))"
        | Some l -> "(( " + var + " >= " + string s.First + " && " + var + " <= " + string l + " ))"
        | None -> "(( " + var + " >= " + string s.First + " ))"

    let private shSpec (s: ValueSpec) =
        (if s.Values.IsEmpty then [] else [ "vals=(" + (s.Values |> List.map sq |> String.concat " ") + ")" ])
        @ (match s.Path with
           | Some p -> [ "hint=" + string p ]
           | None -> [])
        |> String.concat "; "

    let private casePat (id: string) (names: string list) =
        names |> List.map (fun n -> sq (id + "," + n)) |> String.concat "|"

    /// The word-walking loop and value/positional dispatch shared by bash and zsh.
    let private shBody (indent: string) (ctxs: Ctx list) (loopHead: string) : string list =
        let i = indent
        let transitions =
            ctxs
            |> List.collect (fun c ->
                transitionsOf c
                |> List.map (fun (w, child) -> i + "        " + sq (c.CtxId + "," + w) + ") cmd=" + sq child + "; pos=0; continue ;;"))
        let skips =
            ctxs
            |> List.collect (fun c ->
                valueOptions c
                |> List.map (fun a -> i + "        " + casePat c.CtxId (optNames a) + ") skip=1; lastopt=\"$w\"; continue ;;"))
        let values =
            ctxs
            |> List.collect (fun c ->
                optionSpecs c |> List.map (fun (ns, s) -> i + "        " + casePat c.CtxId ns + ") " + shSpec s + " ;;"))
        let positional (c: Ctx) =
            match slots c with
            | [] -> []
            | ss ->
                [ i + "            if [[ \"$cur\" != -* ]]; then" ]
                @ (ss |> List.map (fun s -> i + "                if " + posCond "pos" s + "; then " + shSpec s.Spec + "; fi"))
                @ [ i + "            fi" ]
        let cases =
            ctxs
            |> List.collect (fun c ->
                [ i + "        " + sq c.CtxId + ")"
                  i + "            opts=(" + (flagsOf c |> List.map sq |> String.concat " ") + ")"
                  i + "            subs=(" + (subNames c.Cmd |> List.map sq |> String.concat " ") + ")" ]
                @ positional c
                @ [ i + "            ;;" ])
        [ i + loopHead
          i + "        if [[ \"$w\" == \"=\" && -n \"$lastopt\" ]]; then skip=1; continue; fi"
          i + "        if (( skip )); then skip=0; lastopt=\"\"; continue; fi"
          i + "        case \"$cmd,$w\" in" ]
        @ transitions
        @ [ i + "        esac"; i + "        case \"$cmd,$w\" in" ]
        @ skips
        @ [ i + "        esac"
            i + "        [[ \"$w\" == -* ]] || pos=$((pos+1))"
            i + "    done" ]
        @ [ i + "    if (( skip )); then"; i + "        case \"$cmd,$lastopt\" in" ]
        @ values
        @ [ i + "        esac"; i + "    else"; i + "        case \"$cmd\" in" ]
        @ cases
        @ [ i + "        esac"; i + "    fi" ]

    let private bash (cmd: Command) =
        let f = "_" + ident cmd.Name
        String.concat
            "\n"
            ([ f + "() {"
               "    local cur=\"${COMP_WORDS[COMP_CWORD]}\" cmd=" + sq (ident cmd.Name) + " w i pos=0 skip=0 lastopt=\"\" hint=\"\""
               "    local -a opts=() subs=() vals=()"
               "    COMPREPLY=()" ]
             @ shBody "" (all cmd) "    for ((i=1; i<COMP_CWORD; i++)); do\n        w=\"${COMP_WORDS[i]}\""
             @ [ "    # `--opt=` is split into `--opt` `=` by COMP_WORDBREAKS"
                 "    if (( skip )) && [[ \"$cur\" == \"=\" ]]; then cur=\"\"; fi"
                 "    if (( skip || ${#vals[@]} )) || [[ -n \"$hint\" ]]; then"
                 "        for w in \"${vals[@]}\"; do"
                 "            [[ \"$w\" == \"$cur\"* ]] && COMPREPLY+=( \"$(printf '%q' \"$w\")\" )"
                 "        done"
                 "        if (( ! skip )); then"
                 "            for w in \"${subs[@]}\"; do [[ \"$w\" == \"$cur\"* ]] && COMPREPLY+=( \"$w\" ); done"
                 "        fi"
                 "        local IFS=$'\\n'"
                 "        case \"$hint\" in"
                 "            f) compopt -o filenames 2>/dev/null; COMPREPLY+=( $(compgen -f -- \"$cur\") ) ;;"
                 "            d) compopt -o filenames 2>/dev/null; COMPREPLY+=( $(compgen -d -- \"$cur\") ) ;;"
                 "        esac"
                 "        return 0"
                 "    fi"
                 "    COMPREPLY=( $(compgen -W \"${opts[*]} ${subs[*]}\" -- \"$cur\") )"
                 "}"
                 "complete -F " + f + " -o bashdefault -o default " + cmd.Name
                 "" ])

    let private zsh (cmd: Command) =
        let f = "_" + ident cmd.Name
        String.concat
            "\n"
            ([ "#compdef " + cmd.Name
               f + "() {"
               "    local cur=\"${words[CURRENT]}\" cmd=" + sq (ident cmd.Name) + " w pos=0 skip=0 lastopt=\"\" hint=\"\""
               "    local -a opts subs vals" ]
             @ shBody "" (all cmd) "    for w in \"${(@)words[2,CURRENT-1]}\"; do"
             @ [ "    if (( ! skip )) && [[ \"$cur\" == --*=* ]]; then"
                 "        lastopt=\"${cur%%=*}\"; skip=1; vals=(); hint=\"\""
                 "        compset -P '*='"
                 "        case \"$cmd,$lastopt\" in" ]
             @ (all cmd
                |> List.collect (fun c ->
                    optionSpecs c |> List.map (fun (ns, s) -> "            " + casePat c.CtxId ns + ") " + shSpec s + " ;;")))
             @ [ "        esac"
                 "    fi"
                 "    if (( skip || ${#vals} )) || [[ -n \"$hint\" ]]; then"
                 "        (( ${#vals} )) && compadd -- \"${vals[@]}\""
                 "        (( skip || ! ${#subs} )) || compadd -- \"${subs[@]}\""
                 "        case \"$hint\" in"
                 "            f) _files ;;"
                 "            d) _files -/ ;;"
                 "        esac"
                 "        return 0"
                 "    fi"
                 "    compadd -- \"${opts[@]}\" \"${subs[@]}\""
                 "}"
                 "compdef " + f + " " + cmd.Name
                 "" ])

    // ---------------------------------------------------------------------------------------------
    // fish
    // ---------------------------------------------------------------------------------------------

    let private fish (cmd: Command) =
        let ctxs = all cmd
        let fn = "__" + ident cmd.Name + "_flap_ctx"
        let complete = "complete -c " + cmd.Name
        let cond (args: string) = " -n " + fsq (fn + " " + args)
        let valuesArg (vs: string list) =
            " -a " + fsq ("(printf \"%s\\n\" " + (vs |> List.map fdq |> String.concat " ") + ")")
        let dirs = " -a " + fsq "(__fish_complete_directories (commandline -ct))"
        let desc (h: string option) =
            match h with
            | Some h when oneLine h <> "" -> " -d " + fsq (oneLine h)
            | _ -> ""
        let helper =
            [ "function " + fn
              "    set -l toks (commandline -opc)"
              "    set -e toks[1]"
              "    set -l cmd " + fsq (ident cmd.Name)
              "    set -l pos 0"
              "    set -l skip 0"
              "    for w in $toks"
              "        if test $skip -eq 1"
              "            set skip 0"
              "            continue"
              "        end"
              "        switch \"$cmd,$w\"" ]
            @ (ctxs
               |> List.collect (fun c ->
                   transitionsOf c
                   |> List.collect (fun (w, child) ->
                       [ "            case " + fsq (c.CtxId + "," + w)
                         "                set cmd " + fsq child
                         "                set pos 0"
                         "                continue" ])))
            @ [ "        end"; "        switch \"$cmd,$w\"" ]
            @ (ctxs
               |> List.collect (fun c ->
                   match valueOptions c |> List.collect optNames with
                   | [] -> []
                   | ns ->
                       [ "            case " + (ns |> List.map (fun n -> fsq (c.CtxId + "," + n)) |> String.concat " ")
                         "                set skip 1"
                         "                continue" ]))
            @ [ "        end"
                "        if not string match -q -- '-*' $w"
                "            set pos (math $pos + 1)"
                "        end"
                "    end"
                "    test \"$cmd\" = $argv[1]; or return 1"
                "    set -q argv[2]; or return 0"
                "    test $skip -eq 0; or return 1"
                "    string match -q -- '-*' (commandline -ct); and return 1"
                "    test $pos -ge $argv[2]; or return 1"
                "    test \"$argv[3]\" = inf; or test $pos -le $argv[3]"
                "end" ]
        let perCtx (c: Ctx) =
            let opts =
                visibleOptions c
                |> List.map (fun a ->
                    complete + cond c.CtxId
                    + (a.Short |> Option.map (fun s -> " -s " + string s) |> Option.defaultValue "")
                    + (a.Long |> Option.map (fun l -> " -l " + l) |> Option.defaultValue "")
                    + (a.Aliases |> List.map (fun l -> " -l " + l) |> String.concat "")
                    + desc a.HelpText
                    + (if not (Arg.takesValue a) then ""
                       else
                           match specOf a with
                           | None -> " -r"
                           | Some { Path = Some 'd'; Values = vs } ->
                               " -r -f" + dirs + (if vs.IsEmpty then "" else valuesArg vs)
                           | Some { Path = Some _; Values = vs } -> " -r -F" + (if vs.IsEmpty then "" else valuesArg vs)
                           | Some { Values = vs } -> " -r -f" + valuesArg vs))
            let subs =
                c.Cmd.Subcommands
                |> List.filter (fun s -> not s.HiddenCommand)
                |> List.map (fun s -> complete + cond c.CtxId + " -f -a " + fsq s.Name + desc s.About)
            let positionals =
                slots c
                |> List.collect (fun s ->
                    let range =
                        c.CtxId + " " + string s.First + " " + (match s.Last with Some l -> string l | None -> "inf")
                    (if s.Spec.Values.IsEmpty then [] else [ complete + cond range + " -f" + valuesArg s.Spec.Values ])
                    @ (match s.Spec.Path with
                       | Some 'd' -> [ complete + cond range + " -f" + dirs ]
                       | Some _ -> [ complete + cond range + " -F" ]
                       | None -> []))
            opts @ subs @ positionals
        String.concat "\n" (helper @ (ctxs |> List.collect perCtx)) + "\n"

    // ---------------------------------------------------------------------------------------------
    // PowerShell
    // ---------------------------------------------------------------------------------------------

    let private powershell (cmd: Command) =
        let ctxs = all cmd
        let psArr (xs: string list) = "@(" + (xs |> List.map psq |> String.concat ", ") + ")"
        let psSpec (s: ValueSpec) =
            (if s.Values.IsEmpty then [] else [ "$vals = " + psArr s.Values ])
            @ (match s.Path with
               | Some p -> [ "$hint = " + psq (string p) ]
               | None -> [])
            |> String.concat "; "
        let posCondPs (s: Slot) =
            match s.Last with
            | Some l when l = s.First -> "$pos -eq " + string s.First
            | Some l -> "$pos -ge " + string s.First + " -and $pos -le " + string l
            | None -> "$pos -ge " + string s.First
        let transitions =
            ctxs
            |> List.collect (fun c ->
                transitionsOf c |> List.map (fun (w, child) -> "            " + psq (c.CtxId + "," + w) + " { " + psq child + " }"))
        let skips =
            ctxs
            |> List.collect (fun c ->
                valueOptions c
                |> List.collect optNames
                |> List.map (fun n -> "            " + psq (c.CtxId + "," + n) + " { $true }"))
        let values =
            ctxs
            |> List.collect (fun c ->
                optionSpecs c
                |> List.collect (fun (ns, s) -> ns |> List.map (fun n -> "            " + psq (c.CtxId + "," + n) + " { " + psSpec s + " }")))
        let cases =
            ctxs
            |> List.collect (fun c ->
                [ "            " + psq c.CtxId + " {"
                  "                $opts = " + psArr (flagsOf c)
                  "                $subs = " + psArr (subNames c.Cmd) ]
                @ (match slots c with
                   | [] -> []
                   | ss ->
                       [ "                if (-not $cur.StartsWith('-')) {" ]
                       @ (ss |> List.map (fun s -> "                    if (" + posCondPs s + ") { " + psSpec s.Spec + " }"))
                       @ [ "                }" ])
                @ [ "            }" ])
        String.concat
            "\n"
            ([ "Register-ArgumentCompleter -Native -CommandName " + psq cmd.Name + " -ScriptBlock {"
               "    param($wordToComplete, $commandAst, $cursorPosition)"
               "    function Get-FlapQuoted([string]$s) {"
               "        if ($s -match '[\\s''\"`$;,(){}@|&<>#]') { \"'\" + $s.Replace(\"'\", \"''\") + \"'\" } else { $s }"
               "    }"
               "    $cmd = " + psq (ident cmd.Name)
               "    $pos = 0"
               "    $skip = $false"
               "    $lastopt = ''"
               "    foreach ($e in @($commandAst.CommandElements | Select-Object -Skip 1)) {"
               "        if ($e.Extent.EndOffset -ge $cursorPosition) { break }"
               "        $v = $e.ToString()"
               "        if ($skip) { $skip = $false; $lastopt = ''; continue }"
               "        $next = switch -casesensitive (\"$cmd,$v\") {" ]
             @ transitions
             @ [ "            default { $null }"
                 "        }"
                 "        if ($next) { $cmd = $next; $pos = 0; continue }"
                 "        $isOpt = switch -casesensitive (\"$cmd,$v\") {" ]
             @ skips
             @ [ "            default { $false }"
                 "        }"
                 "        if ($isOpt) { $skip = $true; $lastopt = $v; continue }"
                 "        if (-not $v.StartsWith('-')) { $pos++ }"
                 "    }"
                 "    $cur = \"$wordToComplete\""
                 "    $prefix = ''"
                 "    if (-not $skip -and $cur -like '--*=*') {"
                 "        $i = $cur.IndexOf('=')"
                 "        $lastopt = $cur.Substring(0, $i); $prefix = $cur.Substring(0, $i + 1); $cur = $cur.Substring($i + 1); $skip = $true"
                 "    }"
                 "    $vals = @(); $hint = ''; $opts = @(); $subs = @()"
                 "    if ($skip) {"
                 "        switch -casesensitive (\"$cmd,$lastopt\") {" ]
             @ values
             @ [ "            default { }"; "        }"; "    } else {"; "        switch -casesensitive ($cmd) {" ]
             @ cases
             @ [ "            default { }"
                 "        }"
                 "    }"
                 "    if ($skip -or $vals.Count -gt 0 -or $hint) {"
                 "        foreach ($v in $vals) {"
                 "            if ($v.StartsWith($cur)) { [System.Management.Automation.CompletionResult]::new($prefix + (Get-FlapQuoted $v), $v, 'ParameterValue', $v) }"
                 "        }"
                 "        if (-not $skip) {"
                 "            foreach ($s in $subs) {"
                 "                if ($s.StartsWith($cur)) { [System.Management.Automation.CompletionResult]::new($s, $s, 'ParameterValue', $s) }"
                 "            }"
                 "        }"
                 "        if ($hint) {"
                 "            $items = if ($hint -eq 'd') { Get-ChildItem -Path ($cur + '*') -Directory -ErrorAction SilentlyContinue } else { Get-ChildItem -Path ($cur + '*') -ErrorAction SilentlyContinue }"
                 "            $sep = [Math]::Max($cur.LastIndexOf('/'), $cur.LastIndexOf('\\'))"
                 "            $dir = if ($sep -ge 0) { $cur.Substring(0, $sep + 1) } else { '' }"
                 "            foreach ($it in $items) {"
                 "                $p = $dir + $it.Name"
                 "                $kind = if ($it.PSIsContainer) { 'ProviderContainer' } else { 'ProviderItem' }"
                 "                [System.Management.Automation.CompletionResult]::new($prefix + (Get-FlapQuoted $p), $p, $kind, $p)"
                 "            }"
                 "        }"
                 "        return"
                 "    }"
                 "    ($opts + $subs) | Where-Object { $_.StartsWith($cur) } |"
                 "        ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }"
                 "}"
                 "" ])

    // ---------------------------------------------------------------------------------------------
    // Nushell (extern definitions; `string@"completer"` for fixed values, `path`/`directory` for hints)
    // ---------------------------------------------------------------------------------------------

    /// Nushell double-quoted string.
    let private nq (s: string) =
        "\""
        + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n")
        + "\""

    let private nushell (cmd: Command) =
        let ctxs = all cmd
        let completerName (c: Ctx) (a: Arg) = "nu-complete " + String.concat " " c.Path + " " + a.Id
        let typeOf (c: Ctx) (a: Arg) =
            if not a.PossibleValues.IsEmpty then "string@" + nq (completerName c a)
            else
                match pathOf a with
                | Some 'd' -> "directory"
                | Some _ -> "path"
                | None -> "string"
        let comment (a: Arg) =
            match a.HelpText with
            | Some h when oneLine h <> "" -> " # " + oneLine h
            | _ -> ""
        let flagLine (c: Ctx) (a: Arg) =
            let names =
                match a.Long, a.Short with
                | Some l, Some s -> "--" + l + " (-" + string s + ")"
                | Some l, None -> "--" + l
                | None, Some s -> "-" + string s
                | None, None -> ""
            if names = "" then None
            else Some("    " + names + (if Arg.takesValue a then ": " + typeOf c a else "") + comment a)
        let positionalLines (c: Ctx) =
            c.Args
            |> List.filter (fun a -> a.Index.IsSome && not a.Hidden)
            |> List.sortBy (fun a -> a.Index.Value)
            |> List.fold
                (fun (seenOpt, acc) a ->
                    let name = ident a.Id
                    if a.MaxValues > 1 then true, acc @ [ "    ..." + name + ": " + typeOf c a + comment a ]
                    elif a.Required && not seenOpt then seenOpt, acc @ [ "    " + name + ": " + typeOf c a + comment a ]
                    else true, acc @ [ "    " + name + "?: " + typeOf c a + comment a ])
                (false, [])
            |> snd
        let defs (c: Ctx) =
            visibleOptions c
            @ (c.Args |> List.filter (fun a -> a.Index.IsSome && not a.Hidden))
            |> List.filter (fun a -> not a.PossibleValues.IsEmpty && (a.Index.IsSome || Arg.takesValue a))
            |> List.map (fun a ->
                "def " + nq (completerName c a) + " [] { [ " + (a.PossibleValues |> List.map nq |> String.concat " ") + " ] }")
        let namePaths (c: Ctx) : string list =
            // Walk the tree following c.Path (canonical names) and fan out over aliases at each level below the root.
            let rec go (cur: Command) (names: string list) : string list list =
                match names with
                | [] -> [ [] ]
                | n :: tl ->
                    match cur.Subcommands |> List.tryFind (fun s -> s.Name = n) with
                    | None -> []
                    | Some s ->
                        (s.Name :: s.CommandAliases)
                        |> List.collect (fun spelling -> go s tl |> List.map (fun r -> spelling :: r))
            go cmd c.Path.Tail |> List.map (fun r -> String.concat " " (cmd.Name :: r))
        let rec hiddenPath (cur: Command) (names: string list) =
            match names with
            | [] -> false
            | n :: tl ->
                match cur.Subcommands |> List.tryFind (fun s -> s.Name = n) with
                | Some s -> s.HiddenCommand || hiddenPath s tl
                | None -> false
        let block (c: Ctx) =
            let about =
                match c.Cmd.About with
                | Some a when oneLine a <> "" -> a.Replace("\r", "").Split('\n') |> List.ofArray |> List.map (fun l -> ("# " + l).TrimEnd())
                | _ -> []
            let body = (visibleOptions c |> List.choose (flagLine c)) @ positionalLines c
            defs c
            @ (namePaths c
               |> List.collect (fun p -> about @ [ "export extern " + nq p + " [" ] @ body @ [ "]"; "" ]))
        let visible = ctxs |> List.filter (fun c -> not (hiddenPath cmd c.Path.Tail))
        String.concat "\n" (visible |> List.collect block)

    /// Generate a completion script for the given shell.
    let generate (shell: Shell) (cmd: Command) : string =
        match shell with
        | Bash -> bash cmd
        | Zsh -> zsh cmd
        | Fish -> fish cmd
        | PowerShell -> powershell cmd
        | Nushell -> nushell cmd
