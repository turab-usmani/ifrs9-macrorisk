using Ifrs9Simulator.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ifrs9Simulator.Api.Data;

public class Ifrs9DbContext : DbContext
{
    public Ifrs9DbContext(DbContextOptions<Ifrs9DbContext> options) : base(options)
    {
    }

    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ead).IsRequired();
            entity.Property(e => e.Lgd).IsRequired();
            entity.Property(e => e.BasePd12m).IsRequired();
            entity.Property(e => e.BasePdLifetime).IsRequired();
        });
    }
}
