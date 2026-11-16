# How to use Flap with Native AOT or Fable

## Native AOT

1. Compile your app with `--reflectionfree` so F# does not generate reflection-based `ToString` for `%A`:

   ```xml
   <OtherFlags>$(OtherFlags) --reflectionfree</OtherFlags>
   ```

2. Avoid `%A` / `sprintf "%A"` in your own code.
3. Publish:

   ```
   dotnet publish -c Release -r osx-arm64
   ```

See `samples/Flap.Sample` for a working project, or [Structure an application](structure-an-application.md) for a larger AOT-published example.

## Fable

`Command.parse` uses the console and process environment and is not available under Fable. Use the pure functions instead:

1. Parse with `Command.tryParse` (or `Command.tryParseWith` to supply environment lookup for `Arg.env`).
2. Handle the `Result<Matches, ParseError>` yourself. `ParseError.render` gives the message and `ParseError.exitCode` gives the clap-compatible code.

   ```fsharp
   match Command.tryParseWith env argv cmd with
   | Ok m -> ...
   | Error e -> System.Console.WriteLine(ParseError.render e)
   ```

3. Build:

   ```
   cd samples/Flap.Fable && dotnet fable --lang js
   ```

If you want callbacks for output and exit instead of a `Result`, use `Command.parseWith`.
