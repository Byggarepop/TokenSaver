using Microsoft.Extensions.DependencyInjection;

namespace DiFixture.More;

public interface IWidget { }
public sealed class Widget : IWidget { }
public sealed class KeyedWidget : IWidget { }
public sealed class FactoryWidget : IWidget { }

// A SECOND DI file in the same folder. Uses type names disjoint from Registrations.cs so the
// existing exact-count assertions there are unaffected, while letting these tests prove:
//   * cross-file aggregation (di/ now holds two wiring files),
//   * file-path + line reporting on DiRegistration,
//   * the keyed+typeof and keyed+factory-lambda forms the single-file fixture never exercises.
public static class MoreWiring
{
    public static void Register(IServiceCollection services)
    {
        // Keyed + typeof form: service/impl come from the typeof() args, key from the string literal.
        services.AddKeyedScoped(typeof(IWidget), typeof(KeyedWidget), "kw");

        // Keyed + single-generic + factory lambda: key "fw", impl inferred from new FactoryWidget().
        services.AddKeyedSingleton<IWidget>("fw", (sp, key) => new FactoryWidget());

        // Plain generic registration — the line this test pins FilePath/Line accuracy against.
        services.AddTransient<IWidget, Widget>();

        // De-duplicating keyed variant — TryAddKeyed* are real MS.DI methods and must be recognised.
        services.TryAddKeyedScoped<IWidget, Widget>("tk");
    }
}
