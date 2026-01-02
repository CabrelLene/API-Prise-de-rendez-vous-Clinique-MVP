using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ClinicDbContext db, CancellationToken ct = default)
    {
        // ✅ PostgreSQL/SQL Server => migrations
        // ✅ InMemory (tests) => ensure created
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        // Seed "best effort" sans dépendre des types
        await SeedSetIfEmptyAsync(db, setPropName: "Patients", fixedGuid: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            extraFill: entity =>
            {
                SetIfExists(entity, "FirstName", "John");
                SetIfExists(entity, "LastName", "Doe");
                SetIfExists(entity, "Name", "John Doe");
                SetIfExists(entity, "FullName", "John Doe");
                SetIfExists(entity, "Email", "john.doe@test.com");
                SetIfExists(entity, "Phone", "000-000-0000");
            },
            ct: ct
        );

        await SeedSetIfEmptyAsync(db, setPropName: "Practitioners", fixedGuid: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            extraFill: entity =>
            {
                SetIfExists(entity, "FirstName", "Dr");
                SetIfExists(entity, "LastName", "House");
                SetIfExists(entity, "Name", "Dr House");
                SetIfExists(entity, "FullName", "Dr House");
                SetIfExists(entity, "Specialty", "General");
                SetIfExists(entity, "Email", "dr.house@test.com");
            },
            ct: ct
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSetIfEmptyAsync(
        DbContext db,
        string setPropName,
        Guid fixedGuid,
        Action<object> extraFill,
        CancellationToken ct)
    {
        // Cherche une propriété DbSet<T> nommée "Patients" / "Practitioners"
        var prop = db.GetType().GetProperty(setPropName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null) return;

        var setObj = prop.GetValue(db);
        if (setObj is null) return;

        // Récupère T dans DbSet<T>
        var entityType = prop.PropertyType.GenericTypeArguments.FirstOrDefault();
        if (entityType is null) return;

        // AnyAsync<T>(IQueryable<T>, CancellationToken)
        var queryable = setObj as System.Linq.IQueryable;
        if (queryable is null) return;

        bool any = await AnyAsync(queryable, entityType, ct);
        if (any) return;

        // Création d'une entité vide + remplissage safe
        var entity = Activator.CreateInstance(entityType);
        if (entity is null) return;

        // Set Id si c'est un Guid. (Si int identity => on touche pas)
        var idProp = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp is not null && idProp.CanWrite)
        {
            var idType = Nullable.GetUnderlyingType(idProp.PropertyType) ?? idProp.PropertyType;
            if (idType == typeof(Guid))
                SetIfExists(entity, "Id", fixedGuid);
        }

        extraFill(entity);

        // DbContext.Add(object) marche sans connaître le type générique
        db.Add(entity);
    }

    private static async Task<bool> AnyAsync(System.Linq.IQueryable queryable, Type entityType, CancellationToken ct)
    {
        var methods = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync))
            .ToArray();

        // On veut: AnyAsync<TSource>(IQueryable<TSource>, CancellationToken)
        var anyAsync = methods.First(m =>
        {
            var p = m.GetParameters();
            return m.IsGenericMethodDefinition
                   && p.Length == 2
                   && p[0].ParameterType.IsGenericType
                   && p[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                   && p[1].ParameterType == typeof(CancellationToken);
        });

        var generic = anyAsync.MakeGenericMethod(entityType);

        var taskObj = generic.Invoke(null, new object[] { queryable, ct });
        if (taskObj is Task<bool> t) return await t;

        // fallback ultra-safe
        return false;
    }

    private static void SetIfExists(object target, string propName, object? value)
    {
        var prop = target.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanWrite) return;

        if (value is null)
        {
            // null seulement si nullable / ref type
            var isNullableValueType = Nullable.GetUnderlyingType(prop.PropertyType) is not null;
            if (prop.PropertyType.IsValueType && !isNullableValueType) return;
            prop.SetValue(target, null);
            return;
        }

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        try
        {
            if (targetType.IsEnum && value is string s)
            {
                prop.SetValue(target, Enum.Parse(targetType, s, ignoreCase: true));
                return;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                prop.SetValue(target, value);
                return;
            }

            prop.SetValue(target, Convert.ChangeType(value, targetType));
        }
        catch
        {
            // on ignore si incompatible (seed "best effort" sans casser le build)
        }
    }
}
