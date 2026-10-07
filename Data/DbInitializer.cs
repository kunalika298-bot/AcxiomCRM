using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database is created/migrated
        await context.Database.MigrateAsync();

        // 1. Seed Roles
        string[] roles = ["Admin", "Manager", "Sales Executive"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Users
        var adminEmail = "admin@acxiomcrm.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        var managerEmail = "manager@acxiomcrm.com";
        var managerUser = await userManager.FindByEmailAsync(managerEmail);
        if (managerUser == null)
        {
            managerUser = new ApplicationUser
            {
                UserName = managerEmail,
                Email = managerEmail,
                EmailConfirmed = true,
                FullName = "Sarah Connor (Manager)",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(managerUser, "Manager@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "Manager");
            }
        }

        var salesEmail = "sales@acxiomcrm.com";
        var salesUser = await userManager.FindByEmailAsync(salesEmail);
        if (salesUser == null)
        {
            salesUser = new ApplicationUser
            {
                UserName = salesEmail,
                Email = salesEmail,
                EmailConfirmed = true,
                FullName = "John Doe (Sales Exec)",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(salesUser, "Sales@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(salesUser, "Sales Executive");
            }
        }

        // 3. Seed Sample CRM Data if empty
        if (!context.Customers.Any())
        {
            var cust1 = new Customer
            {
                CustomerCode = "CUST-1001",
                CustomerName = "Acme Corporation",
                Email = "contact@acme.com",
                Phone = "+1-555-0101",
                CompanyName = "Acme Corp Ltd",
                Address = "100 Industrial Parkway",
                City = "New York",
                State = "NY",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddMonths(-3),
                CreatedBy = adminEmail,
                AssignedTo = salesUser?.Id
            };

            var cust2 = new Customer
            {
                CustomerCode = "CUST-1002",
                CustomerName = "Nexus Innovations",
                Email = "info@nexusinno.com",
                Phone = "+1-555-0102",
                CompanyName = "Nexus Tech Inc",
                Address = "450 Silicon Blvd",
                City = "San Jose",
                State = "CA",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddMonths(-2),
                CreatedBy = adminEmail,
                AssignedTo = salesUser?.Id
            };

            var cust3 = new Customer
            {
                CustomerCode = "CUST-1003",
                CustomerName = "Starlight Logistics",
                Email = "dispatch@starlight.com",
                Phone = "+1-555-0103",
                CompanyName = "Starlight Global",
                Address = "88 Harbor Way",
                City = "Chicago",
                State = "IL",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddMonths(-1),
                CreatedBy = managerEmail,
                AssignedTo = managerUser?.Id
            };

            context.Customers.AddRange(cust1, cust2, cust3);
            await context.SaveChangesAsync();

            // Seed Leads
            var lead1 = new Lead
            {
                LeadCode = "LEAD-2001",
                LeadName = "David Miller",
                Email = "david.miller@vanguard.io",
                Phone = "+1-555-0201",
                CompanyName = "Vanguard Tech",
                Source = "Website",
                Status = "Qualified",
                ExpectedValue = 45000m,
                Priority = "High",
                Notes = "Interested in Enterprise CRM deployment for 50 seats.",
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                AssignedTo = salesUser?.Id
            };

            var lead2 = new Lead
            {
                LeadCode = "LEAD-2002",
                LeadName = "Elena Rostova",
                Email = "elena@quantumdata.org",
                Phone = "+1-555-0202",
                CompanyName = "Quantum Data Labs",
                Source = "Referral",
                Status = "New",
                ExpectedValue = 28000m,
                Priority = "Medium",
                Notes = "Referred by Nexus Tech. Needs pricing by end of month.",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                AssignedTo = salesUser?.Id
            };

            var lead3 = new Lead
            {
                LeadCode = "LEAD-2003",
                LeadName = "Marcus Brody",
                Email = "mbrody@zenithbrands.com",
                Phone = "+1-555-0203",
                CompanyName = "Zenith Brands",
                Source = "Cold Call",
                Status = "Contacted",
                ExpectedValue = 15000m,
                Priority = "Low",
                Notes = "Had initial demo call. Follow-up scheduled.",
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                AssignedTo = managerUser?.Id
            };

            var lead4 = new Lead
            {
                LeadCode = "LEAD-2004",
                LeadName = "Rachel Green",
                Email = "rachel@ralphlauren-test.com",
                Phone = "+1-555-0204",
                CompanyName = "Fashion Forward",
                Source = "Partner",
                Status = "Converted",
                ExpectedValue = 60000m,
                Priority = "High",
                Notes = "Converted to Acme Corporation account.",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                AssignedTo = salesUser?.Id,
                ConvertedCustomerId = cust1.CustomerId
            };

            context.Leads.AddRange(lead1, lead2, lead3, lead4);
            await context.SaveChangesAsync();

            // Seed Opportunities
            var opp1 = new Opportunity
            {
                OpportunityName = "Acme Annual Cloud License Renewal",
                CustomerId = cust1.CustomerId,
                Amount = 75000m,
                Stage = "Negotiation",
                Probability = 70m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                AssignedTo = salesUser?.Id,
                Source = "Existing Client",
                Notes = "Contract redlining in progress."
            };

            var opp2 = new Opportunity
            {
                OpportunityName = "Nexus CRM Migration Project",
                CustomerId = cust2.CustomerId,
                Amount = 120000m,
                Stage = "Proposal",
                Probability = 50m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-12),
                AssignedTo = salesUser?.Id,
                Source = "Inbound",
                Notes = "Submitted RFP response."
            };

            var opp3 = new Opportunity
            {
                OpportunityName = "Starlight Logistics Fleet Portal",
                CustomerId = cust3.CustomerId,
                Amount = 95000m,
                Stage = "Won",
                Probability = 100m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
                Status = "Won",
                CreatedDate = DateTime.UtcNow.AddDays(-45),
                AssignedTo = managerUser?.Id,
                Source = "Partner",
                Notes = "Deal closed successfully! Initial payment received."
            };

            var opp4 = new Opportunity
            {
                OpportunityName = "Vanguard Pilot Deployment",
                LeadId = lead1.LeadId,
                Amount = 30000m,
                Stage = "Qualification",
                Probability = 30m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(50),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-3),
                AssignedTo = salesUser?.Id,
                Source = "Website",
                Notes = "Discovery workshop planned."
            };

            context.Opportunities.AddRange(opp1, opp2, opp3, opp4);
            await context.SaveChangesAsync();

            // Seed FollowUps
            var fu1 = new FollowUp
            {
                CustomerId = cust1.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(2),
                FollowUpType = "Meeting",
                Remarks = "Discuss final contract clause revisions with procurement.",
                Status = "Planned",
                AssignedTo = salesUser?.Id
            };

            var fu2 = new FollowUp
            {
                LeadId = lead2.LeadId,
                FollowUpDate = DateTime.UtcNow.AddDays(4),
                FollowUpType = "Call",
                Remarks = "Call Elena regarding budget confirmation.",
                Status = "Planned",
                AssignedTo = salesUser?.Id
            };

            var fu3 = new FollowUp
            {
                CustomerId = cust2.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(-2),
                FollowUpType = "Email",
                Remarks = "Check if legal finished review.",
                Status = "Missed",
                AssignedTo = salesUser?.Id
            };

            var fu4 = new FollowUp
            {
                CustomerId = cust3.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(-6),
                FollowUpType = "Meeting",
                Remarks = "Kickoff meeting for project onboarding.",
                Status = "Completed",
                AssignedTo = managerUser?.Id
            };

            context.FollowUps.AddRange(fu1, fu2, fu3, fu4);

            // Seed Activities
            var act1 = new Activity
            {
                ActivityType = "Meeting",
                Subject = "Architecture Review Session",
                Description = "Met with engineering lead to finalize SSO integrations.",
                ActivityDate = DateTime.UtcNow.AddDays(-4),
                CustomerId = cust1.CustomerId,
                AssignedTo = salesUser?.Id,
                Status = "Completed"
            };

            var act2 = new Activity
            {
                ActivityType = "Call",
                Subject = "Introductory Discovery Call",
                Description = "Identified pain points in their legacy CRM system.",
                ActivityDate = DateTime.UtcNow.AddDays(-8),
                LeadId = lead1.LeadId,
                AssignedTo = salesUser?.Id,
                Status = "Completed"
            };

            var act3 = new Activity
            {
                ActivityType = "Email",
                Subject = "Sent Proposal & Pricing Matrix",
                Description = "Forwarded official proposal document to stakeholders.",
                ActivityDate = DateTime.UtcNow.AddDays(-2),
                CustomerId = cust2.CustomerId,
                AssignedTo = salesUser?.Id,
                Status = "Completed"
            };

            context.Activities.AddRange(act1, act2, act3);

            // Seed Initial Audit Log
            var log = new AuditLog
            {
                Action = "System Initialized",
                EntityName = "Database",
                RecordId = "0",
                NewValue = "Seeded initial roles, users, and CRM sample records.",
                UserId = adminUser?.Id,
                UserEmail = adminEmail,
                CreatedDate = DateTime.UtcNow,
                IpAddress = "127.0.0.1"
            };
            context.AuditLogs.Add(log);

            await context.SaveChangesAsync();
        }
    }
}
