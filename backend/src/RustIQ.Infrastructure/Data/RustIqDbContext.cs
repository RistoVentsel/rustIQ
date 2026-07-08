using Microsoft.EntityFrameworkCore;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.Data;

public sealed class RustIqDbContext(DbContextOptions<RustIqDbContext> options)
    : DbContext(options)
{
    public DbSet<Server> Servers => Set<Server>();

    public DbSet<ServerSnapshot> ServerSnapshots => Set<ServerSnapshot>();

    public DbSet<ServerPlayerCurrent> ServerPlayersCurrent => Set<ServerPlayerCurrent>();

    public DbSet<ServerMapMetadata> ServerMapMetadata => Set<ServerMapMetadata>();

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

            entity.HasMany(server => server.Snapshots)
                .WithOne(snapshot => snapshot.Server)
                .HasForeignKey(snapshot => snapshot.ServerId)
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

            entity.HasIndex(snapshot => new { snapshot.ServerId, snapshot.CheckedAtUtc });
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
    }
}
