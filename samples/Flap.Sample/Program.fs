open Flap

[<EntryPoint>]
let main argv =
    let cmd =
        Command.create "sample"
        |> Command.version "0.1.0"
        |> Command.about "Flap sample (Native AOT)"
        |> Command.arg (Arg.create "name" |> Arg.short 'n' |> Arg.long "name" |> Arg.help "Who to greet" |> Arg.defaultValue "world")
        |> Command.arg (Arg.create "loud" |> Arg.short 'l' |> Arg.long "loud" |> Arg.flag |> Arg.help "Shout")
        |> Command.arg (Arg.create "times" |> Arg.short 't' |> Arg.long "times" |> Arg.defaultValue "1" |> Arg.validate (fun s -> Value.int s |> Result.map ignore))

    let m = cmd |> Command.parse (List.ofArray argv)
    let name = Matches.getString "name" m
    match Matches.tryInt "times" m with
    | Error e ->
        System.Console.Error.WriteLine("error: " + e)
        2
    | Ok t ->
        let times = t |> Option.defaultValue 1
        for _ in 1..times do
            let msg = "Hello, " + name + "!"
            System.Console.WriteLine(if Matches.flag "loud" m then msg.ToUpperInvariant() else msg)
        0
