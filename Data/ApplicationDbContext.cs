using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Decimal precisions
        builder.Entity<Opportunity>()
            .Property(o => o.Amount)
            .HasPrecision(18, 2);

        builder.Entity<Opportunity>()
            .Property(o => o.Probability)
            .HasPrecision(5, 2);

        builder.Entity<Lead>()
            .Property(l => l.ExpectedValue)
            .HasPrecision(18, 2);

        // Unique indexes
        builder.Entity<Customer>()
            .HasIndex(c => c.CustomerCode)
            .IsUnique();

        builder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        builder.Entity<Lead>()
            .HasIndex(l => l.LeadCode)
            .IsUnique();

        // Customer relationships
        builder.Entity<Customer>()
            .HasOne(c => c.AssignedUser)
            .WithMany(u => u.AssignedCustomers)
            .HasForeignKey(c => c.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        // Lead relationships
        builder.Entity<Lead>()
            .HasOne(l => l.AssignedUser)
            .WithMany(u => u.AssignedLeads)
            .HasForeignKey(l => l.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Lead>()
            .HasOne(l => l.ConvertedCustomer)
            .WithMany()
            .HasForeignKey(l => l.ConvertedCustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Opportunity relationships
        builder.Entity<Opportunity>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Opportunities)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Opportunity>()
            .HasOne(o => o.Lead)
            .WithMany(l => l.Opportunities)
            .HasForeignKey(o => o.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Opportunity>()
            .HasOne(o => o.AssignedUser)
            .WithMany(u => u.AssignedOpportunities)
            .HasForeignKey(o => o.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        // FollowUp relationships
        builder.Entity<FollowUp>()
            .HasOne(f => f.Customer)
            .WithMany(c => c.FollowUps)
            .HasForeignKey(f => f.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FollowUp>()
            .HasOne(f => f.Lead)
            .WithMany(l => l.FollowUps)
            .HasForeignKey(f => f.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FollowUp>()
            .HasOne(f => f.Opportunity)
            .WithMany(o => o.FollowUps)
            .HasForeignKey(f => f.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FollowUp>()
            .HasOne(f => f.AssignedUser)
            .WithMany(u => u.AssignedFollowUps)
            .HasForeignKey(f => f.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        // Activity relationships
        builder.Entity<Activity>()
            .HasOne(a => a.Customer)
            .WithMany(c => c.Activities)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Activity>()
            .HasOne(a => a.Lead)
            .WithMany(l => l.Activities)
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Activity>()
            .HasOne(a => a.AssignedUser)
            .WithMany(u => u.AssignedActivities)
            .HasForeignKey(a => a.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
