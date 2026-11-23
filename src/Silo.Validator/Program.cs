using Silo.Styling;
using Silo.Validation;

// Usage: silo-validate [--context <file.jsonc>]... [--controls <assembly.dll>]... <file.jsonc>...
// Checks Stylesheets against the Controls and prints every problem as "file(line,column): error SILO001: message", the format
// that MSBuild and IDEs understand. --context names Stylesheets that the files are used together with, such as the default
// theme; their Tokens count as declared and they are not checked. --controls names an assembly with more Controls, such as an
// add-on library, so that Selectors may name them. The exit code is 1 when there is an error.
var context = new List<Stylesheet>();
var files = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    if (args[i] is not ("--context" or "--controls"))
    {
        files.Add(args[i]);

        continue;
    }

    var option = args[i];

    if (++i == args.Length)
    {
        Console.Error.WriteLine($"silo-validate: {option} needs a file.");

        return 2;
    }

    if (option == "--controls")
        StylesheetValidator.AddControls(System.Reflection.Assembly.LoadFrom(Path.GetFullPath(args[i])));
    else
        context.Add(JsoncStylesheetReader.Read(File.ReadAllText(args[i]), args[i]));
}

if (files.Count == 0)
{
    Console.Error.WriteLine("Usage: silo-validate [--context <file.jsonc>]... [--controls <assembly.dll>]... <file.jsonc>...");

    return 2;
}

var errors = 0;

foreach (var file in files)
{
    foreach (var issue in StylesheetValidator.ValidateText(File.ReadAllText(file), file, context))
    {
        Console.WriteLine(issue);

        if (issue.Severity == ValidationSeverity.Error)
            errors++;
    }
}

return errors == 0 ? 0 : 1;
