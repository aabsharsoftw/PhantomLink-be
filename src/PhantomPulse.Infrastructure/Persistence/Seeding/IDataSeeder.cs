namespace PhantomPulse.Infrastructure.Persistence.Seeding;

public interface IDataSeeder
{
    /// <summary>Execution order — lower runs first.</summary>
    int Order { get; }

    /// <summary>When true, the seeder is skipped in non-Development environments.</summary>
    bool IsDemoOnly { get; }

    Task SeedAsync(CancellationToken ct = default);
}
