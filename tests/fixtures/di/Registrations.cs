using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DiFixture;

public interface IFoo { }
public sealed class Foo : IFoo { }
public sealed class CachedFoo : IFoo { }
public sealed class DefaultFoo : IFoo { }
public sealed class LambdaFoo : IFoo { }
public sealed class BareFoo { }
public sealed class SoloFoo { }

public interface IRepo<T> { }
public sealed class Repo<T> : IRepo<T> { }
public sealed class User { }

public interface IBar { }
public sealed class Bar : IBar { }
public static class ServiceKeys { public const string Cache = "cache"; }

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

        // Single-generic, no factory — impl falls back to the service type (BareFoo -> BareFoo).
        services.AddSingleton<BareFoo>();

        // Single typeof() arg — impl falls back to the service type (SoloFoo -> SoloFoo).
        services.AddTransient(typeof(SoloFoo));

        // Generic type arguments on the service/impl — simple names IRepo -> Repo.
        services.AddScoped<IRepo<User>, Repo<User>>();

        // Keyed registration with a non-string (member-access) key — IBar -> Bar, key "Cache".
        services.AddKeyedSingleton<IBar, Bar>(ServiceKeys.Cache);

        // Fully-qualified type args — only the simple names survive (IQual -> Qual).
        services.AddScoped<App.IQual, App.Qual>();

        // Decoy: NOT a DI registration even though the method name starts with "Add".
        var names = new List<string>();
        names.AddRange(new[] { "IFoo", "Foo" });
    }
}
