using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiFixture;

public interface IFoo { }
public sealed class Foo : IFoo { }
public sealed class CachedFoo : IFoo { }
public sealed class DefaultFoo : IFoo { }
public sealed class LambdaFoo : IFoo { }

public static class Wiring
{
    public static void Register(IServiceCollection services)
    {
        // Generic 2-arg form.
        services.AddScoped<IFoo, Foo>();

        // typeof() form.
        services.AddTransient(typeof(IFoo), typeof(DefaultFoo));

        // Keyed registration with a string literal key.
        services.AddKeyedScoped<IFoo, CachedFoo>("cache");

        // TryAdd variant (de-duplicating registration).
        services.TryAddSingleton<IFoo, Foo>();

        // Single-generic + factory lambda — impl inferred from the created type.
        services.AddSingleton<IFoo>(sp => new LambdaFoo());

        // Decoy: NOT a DI registration even though the method name starts with "Add".
        var names = new List<string>();
        names.AddRange(new[] { "IFoo", "Foo" });
    }
}
