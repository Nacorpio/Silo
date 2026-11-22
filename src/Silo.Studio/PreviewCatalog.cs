using System.Reflection;
using System.Runtime.Loader;

namespace Silo.Studio;

/// <summary>A method marked with <see cref="PreviewAttribute"/>.</summary>
/// <param name="Name">The name that Studio lists.</param>
/// <param name="TypeName">The full name of the type that has the method.</param>
/// <param name="MethodName">The name of the method.</param>
public sealed record PreviewInfo(string Name, string TypeName, string MethodName);

/// <summary>
/// Finds the Previews in an assembly and runs them. Studio uses it in the child process that shows a Preview, so that code
/// from the user's assembly never runs in Studio itself.
/// </summary>
/// <remarks>
/// <para>
/// A user assembly is loaded into its own <see cref="AssemblyLoadContext"/> that resolves its dependencies from the folder it
/// is in, using its <c>.deps.json</c>. Silo's own assemblies are shared with Studio, so the Controls a Preview returns are the
/// ones Studio draws.
/// </para>
/// <para>A method counts as a Preview when it is static, has no parameters, and returns an <see cref="Element"/>.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var catalog = PreviewCatalog.Load("bin/Debug/net11.0/MyApp.dll");
///
/// foreach (var preview in catalog.Previews)
///     Console.WriteLine(preview.Name);
///
/// var element = catalog.Create("Primary button");
/// </code>
/// </example>
public sealed class PreviewCatalog
{
    /// <summary>The binding flags that find static methods of any visibility.</summary>
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;


    /// <summary>The assembly that has the Previews.</summary>
    readonly Assembly assembly;

    /// <summary>Creates a catalog for an assembly.</summary>
    /// <param name="assembly">The assembly to search.</param>
    public PreviewCatalog(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        this.assembly = assembly;

        Previews = [.. Find(assembly)];
    }

    /// <summary>Gets the Previews, in the order of their names.</summary>
    public IReadOnlyList<PreviewInfo> Previews { get; }

    /// <summary>Loads an assembly from a file and finds its Previews.</summary>
    /// <param name="path">The path of the assembly.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="path"/>.</exception>
    public static PreviewCatalog Load(string path)
    {
        var full = Path.GetFullPath(path);

        if (!File.Exists(full))
            throw new FileNotFoundException("The assembly was not found.", full);

        var context = new PreviewLoadContext(full);

        return new(context.LoadFromAssemblyPath(full));
    }

    /// <summary>Runs a Preview.</summary>
    /// <param name="name">The name of the Preview, from <see cref="PreviewInfo.Name"/>. The case is ignored.</param>
    /// <returns>The Element that the method returned.</returns>
    /// <exception cref="ArgumentException">There is no Preview with that name.</exception>
    /// <exception cref="InvalidOperationException">The method threw or returned <see langword="null"/>.</exception>
    public Element Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var info = Previews.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"There is no Preview named '{name}'.", nameof(name));

        var method = assembly.GetType(info.TypeName)!.GetMethod(info.MethodName, Flags)!;

        try
        {
            return method.Invoke(null, null) as Element
                ?? throw new InvalidOperationException($"The Preview '{info.Name}' returned null.");
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            var message = $"The Preview '{info.Name}' threw: {e.InnerException.Message}";

            throw new InvalidOperationException(message, e.InnerException);
        }
    }

    /// <summary>Finds the methods marked as Previews.</summary>
    /// <param name="assembly">The assembly to search.</param>
    /// <returns>The Previews, ordered by name.</returns>
    static IEnumerable<PreviewInfo> Find(Assembly assembly)
    {
        var found = new List<PreviewInfo>();

        foreach (var type in SafeTypes(assembly))
        foreach (var method in type.GetMethods(Flags))
        {
            var attribute = method.GetCustomAttribute<PreviewAttribute>();

            var usable = method.GetParameters().Length == 0 && typeof(Element).IsAssignableFrom(method.ReturnType);

            if (attribute is null || !usable)
                continue;

            found.Add(new(attribute.Name ?? method.Name, type.FullName!, method.Name));
        }

        return found.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the types of an assembly, leaving out the ones that cannot be loaded.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The types.</returns>
    static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }

    /// <summary>Loads a user assembly and its dependencies, and shares Silo's assemblies with Studio.</summary>
    /// <param name="path">The path of the user assembly.</param>
    sealed class PreviewLoadContext(string path) : AssemblyLoadContext(isCollectible: false)
    {
        /// <summary>Finds dependencies next to the assembly.</summary>
        readonly AssemblyDependencyResolver resolver = new(path);

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null && assemblyName.Name.StartsWith("Silo.", StringComparison.Ordinal))
                return null;

            var file = resolver.ResolveAssemblyToPath(assemblyName);

            return file is null ? null : LoadFromAssemblyPath(file);
        }
    }
}
