using Silo.Styling;
using Silo.Validation;

// Usage: silo-validate [--context <file.jsonc>]... <file.jsonc>...
// Checks Stylesheets against the Controls and prints every problem as "file(line,column): error SILO001: message", the format
// that MSBuild and IDEs understand. --context names Stylesheets that the files are used together with, such as the default
// theme; their Tokens count as declared and they are not checked. The exit code is 1 when there is an error.
var context = new List<Stylesheet>();
var files = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    if (args[i] != "--context")
    {
        files.Add(args[i]);

        continue;
    }

    if (++i == args.Length)
    {
        Console.Error.WriteLine("silo-validate: --context needs a file.");

        return 2;
    }

    context.Add(JsoncStylesheetReader.Read(File.ReadAllText(args[i]), args[i]));
}

if (files.Count == 0)
{
    Console.Error.WriteLine("Usage: silo-validate [--context <file.jsonc>]... <file.jsonc>...");

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
