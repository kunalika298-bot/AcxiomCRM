namespace AcxiomCRM.Models.ViewModels;

public class DashboardViewModel
{
    // KPIs
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public decimal TotalWonRevenue { get; set; }

    // Chart Data - Leads by Status
    public List<string> LeadStatusLabels { get; set; } = new();
    public List<int> LeadStatusCounts { get; set; } = new();

    // Chart Data - Opportunities by Stage
    public List<string> OpportunityStageLabels { get; set; } = new();
    public List<decimal> OpportunityStageValues { get; set; } = new();

    // Chart Data - Monthly Won Sales
    public List<string> MonthlySalesLabels { get; set; } = new();
    public List<decimal> MonthlySalesValues { get; set; } = new();

    // Upcoming / Overdue Follow-Ups for quick executive view
    public List<FollowUp> UrgentFollowUps { get; set; } = new();
    public List<Opportunity> HighValueOpportunities { get; set; } = new();

    // Filter properties
    public string DateRange { get; set; } = "all"; // all, month, quarter, year
}
