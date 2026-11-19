using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Data;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<OperationDefinition> Operations => Set<OperationDefinition>();
    public DbSet<OperationExecution> OperationExecutions => Set<OperationExecution>();
    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationDefinition>(entity =>
        {
            entity.ToTable("Operations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.SortOrder });
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.OperationType).HasMaxLength(40);
            entity.Property(x => x.ImplementationType).HasMaxLength(40);
            entity.Property(x => x.ExecutionMode).HasMaxLength(20);
            entity.Property(x => x.OutputType).HasMaxLength(40);
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.Icon).HasMaxLength(80);
            entity.Property(x => x.InputAConfigJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.InputBConfigJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ConfigurationJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<OperationExecution>(entity =>
        {
            entity.ToTable("OperationExecutions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OperationId, x.ExecutedAtUtc });
            entity.HasIndex(x => new { x.OperationTypeSnapshot, x.ExecutedAtUtc });
            entity.Property(x => x.OperationCodeSnapshot).HasMaxLength(120);
            entity.Property(x => x.OperationNameSnapshot).HasMaxLength(160);
            entity.Property(x => x.OperationTypeSnapshot).HasMaxLength(40);
            entity.Property(x => x.ImplementationTypeSnapshot).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.InputA).HasColumnType("nvarchar(max)");
            entity.Property(x => x.InputB).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            entity.Property(x => x.ExecutionTraceJson).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.Operation).WithMany(x => x.Executions)
                .HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApplicationLog>(entity =>
        {
            entity.ToTable("ApplicationLogs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.Property(x => x.Level).HasMaxLength(20);
            entity.Property(x => x.Source).HasMaxLength(120);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.CorrelationId).HasMaxLength(80);
            entity.Property(x => x.RequestMethod).HasMaxLength(12);
            entity.Property(x => x.RequestPath).HasMaxLength(500);
            entity.Property(x => x.RequestHeadersJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResponseHeadersJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Exception).HasColumnType("nvarchar(max)");
        });
    }
}
