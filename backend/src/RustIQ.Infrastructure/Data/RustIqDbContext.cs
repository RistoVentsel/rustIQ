using Microsoft.EntityFrameworkCore;
using RustIQ.Application.Study;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Infrastructure.Data;

public sealed class RustIqDbContext(DbContextOptions<RustIqDbContext> options)
    : DbContext(options)
{
    public DbSet<Server> Servers => Set<Server>();

    public DbSet<ServerSnapshot> ServerSnapshots => Set<ServerSnapshot>();

    public DbSet<ServerRanking> ServerRankings => Set<ServerRanking>();

    public DbSet<ServerPlayerCurrent> ServerPlayersCurrent => Set<ServerPlayerCurrent>();

    public DbSet<ServerMapMetadata> ServerMapMetadata => Set<ServerMapMetadata>();

    public DbSet<StudySampleServer> StudySampleServers => Set<StudySampleServer>();

    public DbSet<StudyProbe> StudyProbes => Set<StudyProbe>();

    public DbSet<StudyWipeSignal> StudyWipeSignals => Set<StudyWipeSignal>();

    public DbSet<StudyBattleMetricsWipeSnapshot> StudyBattleMetricsWipeSnapshots => Set<StudyBattleMetricsWipeSnapshot>();

    public DbSet<ServerWipeObservation> ServerWipeObservations => Set<ServerWipeObservation>();

    public DbSet<StudyResourceSample> StudyResourceSamples => Set<StudyResourceSample>();

    public DbSet<StudyPopulationSample> StudyPopulationSamples => Set<StudyPopulationSample>();

    public DbSet<StudyTableWriteSample> StudyTableWriteSamples => Set<StudyTableWriteSample>();

    public DbSet<StudySampleRanking> StudySampleRankings => Set<StudySampleRanking>();

    public DbSet<StudyServerDescription> StudyServerDescriptions => Set<StudyServerDescription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Server>(entity =>
        {
            entity.ToTable("servers");

            entity.HasKey(server => server.Id);

            entity.Property(server => server.Id)
                .ValueGeneratedNever();

            entity.Property(server => server.Host)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(server => server.Address)
                .HasMaxLength(255);

            entity.Property(server => server.Name)
                .HasMaxLength(500);

            entity.Property(server => server.Map)
                .HasMaxLength(255);

            entity.Property(server => server.Country)
                .HasMaxLength(8);

            entity.Property(server => server.Latitude);

            entity.Property(server => server.Longitude);

            entity.Property(server => server.ServerType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.DiscoverySource)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.SummarySource)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.PlayerCountSource)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.PlayerListSource)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.DataQuality)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(server => server.LastValidationReason)
                .HasMaxLength(255);

            entity.HasIndex(server => new { server.Host, server.QueryPort })
                .IsUnique();

            entity.HasIndex(server => server.BattleMetricsServerId)
                .IsUnique();

            entity.HasIndex(server => server.Players);

            entity.HasIndex(server => server.Status);

            entity.HasIndex(server => server.ServerType);

            entity.HasIndex(server => server.LastWipeAtUtc);

            entity.HasIndex(server => server.NextWipeAtUtc);

            entity.HasIndex(server => server.Country);

            entity.HasMany(server => server.Snapshots)
                .WithOne(snapshot => snapshot.Server)
                .HasForeignKey(snapshot => snapshot.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany<ServerRanking>()
                .WithOne(ranking => ranking.Server)
                .HasForeignKey(ranking => ranking.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(server => server.CurrentPlayers)
                .WithOne(player => player.Server)
                .HasForeignKey(player => player.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(server => server.MapMetadata)
                .WithOne(metadata => metadata.Server)
                .HasForeignKey<ServerMapMetadata>(metadata => metadata.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServerSnapshot>(entity =>
        {
            entity.ToTable("server_snapshots");

            entity.HasKey(snapshot => snapshot.Id);

            entity.Property(snapshot => snapshot.Id)
                .ValueGeneratedNever();

            entity.Property(snapshot => snapshot.PollSource)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(snapshot => new { snapshot.ServerId, snapshot.CheckedAtUtc });
        });

        modelBuilder.Entity<ServerRanking>(entity =>
        {
            entity.ToTable("server_rankings");

            entity.HasKey(ranking => ranking.Id);

            entity.Property(ranking => ranking.Id)
                .ValueGeneratedNever();

            entity.HasIndex(ranking => ranking.ServerId)
                .IsUnique();

            entity.HasIndex(ranking => ranking.Rank)
                .IsUnique();
        });

        modelBuilder.Entity<ServerPlayerCurrent>(entity =>
        {
            entity.ToTable("server_players_current");

            entity.HasKey(player => player.Id);

            entity.Property(player => player.Id)
                .ValueGeneratedNever();

            entity.Property(player => player.PlayerName)
                .HasMaxLength(255);

            entity.HasIndex(player => new { player.ServerId, player.SeenAtUtc });
        });

        modelBuilder.Entity<ServerMapMetadata>(entity =>
        {
            entity.ToTable("server_map_metadata");

            entity.HasKey(metadata => metadata.Id);

            entity.Property(metadata => metadata.Id)
                .ValueGeneratedNever();

            entity.Property(metadata => metadata.MapName)
                .HasMaxLength(255);

            entity.Property(metadata => metadata.RustMapsUrl)
                .HasMaxLength(1000);

            entity.Property(metadata => metadata.ThumbnailUrl)
                .HasMaxLength(1000);

            entity.Property(metadata => metadata.MapFileUrl)
                .HasMaxLength(1000);

            entity.Property(metadata => metadata.Source)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(metadata => metadata.ServerId)
                .IsUnique();
        });

        // Study tables carry no foreign key to servers on purpose: retention deletes server rows,
        // and a cascade would take the thesis measurements with them.
        modelBuilder.Entity<StudySampleServer>(entity =>
        {
            entity.ToTable("study_sample_servers");

            entity.HasKey(server => server.ServerId);

            entity.Property(server => server.ServerId)
                .ValueGeneratedNever();

            entity.Property(server => server.Host)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(server => server.Name)
                .HasMaxLength(500);

            entity.Property(server => server.Country)
                .HasMaxLength(8);

            entity.Property(server => server.Stratum)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
        });

        modelBuilder.Entity<StudyProbe>(entity =>
        {
            entity.ToTable("study_probes");

            entity.HasKey(probe => probe.Id);

            entity.HasIndex(probe => new { probe.ServerId, probe.ProbedAtUtc });
        });

        modelBuilder.Entity<StudyWipeSignal>(entity =>
        {
            entity.ToTable("study_wipe_signals");

            entity.HasKey(signal => signal.Id);

            entity.Property(signal => signal.Keywords)
                .HasMaxLength(2048);

            entity.HasIndex(signal => new { signal.ServerId, signal.ObservedAtUtc });
        });

        modelBuilder.Entity<StudyBattleMetricsWipeSnapshot>(entity =>
        {
            entity.ToTable("study_battlemetrics_wipe_snapshots");

            entity.HasKey(snapshot => snapshot.Id);

            entity.HasIndex(snapshot => new { snapshot.ServerId, snapshot.CapturedAtUtc });
        });

        modelBuilder.Entity<ServerWipeObservation>(entity =>
        {
            entity.ToTable("study_server_wipe_observations");

            entity.HasKey(observation => observation.Id);

            entity.Property(observation => observation.Keywords)
                .HasMaxLength(StudyKeywords.MaximumLength);

            entity.HasIndex(observation => new { observation.ServerId, observation.ObservedAtUtc });
        });

        modelBuilder.Entity<StudyResourceSample>(entity =>
        {
            entity.ToTable("study_resource_samples");

            entity.HasKey(sample => sample.Id);

            entity.Property(sample => sample.Role)
                .HasMaxLength(32)
                .IsRequired();

            entity.HasIndex(sample => new { sample.Role, sample.SampledAtUtc });
        });

        modelBuilder.Entity<StudyPopulationSample>(entity =>
        {
            entity.ToTable("study_population_samples");

            entity.HasKey(sample => sample.Id);

            entity.HasIndex(sample => sample.SampledAtUtc);
        });

        modelBuilder.Entity<StudyTableWriteSample>(entity =>
        {
            entity.ToTable("study_table_write_samples");

            entity.HasKey(sample => sample.Id);

            entity.Property(sample => sample.TableName)
                .HasMaxLength(128)
                .IsRequired();

            entity.HasIndex(sample => new { sample.TableName, sample.SampledAtUtc });
        });

        modelBuilder.Entity<StudySampleRanking>(entity =>
        {
            entity.ToTable("study_sample_rankings");

            entity.HasKey(ranking => ranking.Id);

            entity.HasIndex(ranking => new { ranking.ServerId, ranking.SampledAtUtc });
        });

        modelBuilder.Entity<StudyServerDescription>(entity =>
        {
            entity.ToTable("study_server_descriptions");

            entity.HasKey(description => description.Id);

            entity.Property(description => description.Description)
                .HasMaxLength(8192);

            entity.HasIndex(description => new { description.ServerId, description.CapturedAtUtc });
        });
    }
}
