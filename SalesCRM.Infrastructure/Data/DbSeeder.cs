using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Infrastructure.Services;

namespace SalesCRM.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (context.Users.Any()) return;

        // Regions
        var west = new Region { Name = "West" };
        var south = new Region { Name = "South" };
        var north = new Region { Name = "North" };
        var east = new Region { Name = "East" };
        var northEast = new Region { Name = "North East" };
        context.Regions.AddRange(west, south, north, east, northEast);
        await context.SaveChangesAsync();

        // Zones
        var mumbaiWest = new Zone { Name = "Mumbai West", RegionId = west.Id };
        var mumbaiEast = new Zone { Name = "Mumbai East", RegionId = west.Id };
        var puneCentral = new Zone { Name = "Pune Central", RegionId = west.Id };
        var puneNorth = new Zone { Name = "Pune North", RegionId = west.Id };
        var nashik = new Zone { Name = "Nashik", RegionId = west.Id };
        context.Zones.AddRange(mumbaiWest, mumbaiEast, puneCentral, puneNorth, nashik);
        await context.SaveChangesAsync();

        // Users
        var users = new[]
        {
            new User { Name = "Arjun Mehta",  Email = "arjun@educrm.in",  PasswordHash = AuthService.HashPassword("fo123"),  Role = UserRole.FO, ZoneId = mumbaiWest.Id, RegionId = west.Id, Avatar = "AM" },
            new User { Name = "Sunita Reddy", Email = "sunita@educrm.in", PasswordHash = AuthService.HashPassword("fo123"),  Role = UserRole.FO, ZoneId = mumbaiWest.Id, RegionId = west.Id, Avatar = "SR" },
            new User { Name = "Vikram Nair",  Email = "vikram@educrm.in", PasswordHash = AuthService.HashPassword("fo123"),  Role = UserRole.FO, ZoneId = puneCentral.Id, RegionId = west.Id, Avatar = "VN" },
            new User { Name = "Priya Singh",  Email = "priya@educrm.in",  PasswordHash = AuthService.HashPassword("zh123"),  Role = UserRole.ZH, ZoneId = mumbaiWest.Id, RegionId = west.Id, Avatar = "PS" },
            new User { Name = "Rajesh Kumar", Email = "rajesh@educrm.in", PasswordHash = AuthService.HashPassword("rh123"),  Role = UserRole.RH, RegionId = west.Id, Avatar = "RK" },
            new User { Name = "Anita Sharma", Email = "anita@educrm.in",  PasswordHash = AuthService.HashPassword("sh123"),  Role = UserRole.SH, Avatar = "AS" },
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var arjun = users[0]; var sunita = users[1]; var vikram = users[2];

        // Leads
        var leads = new[]
        {
            new Lead
            {
                School = "DPS Andheri", Board = "CBSE", City = "Mumbai", State = "Maharashtra",
                Students = 1200, Type = "Private", Stage = LeadStage.DemoDone, Score = 82, Value = 480000,
                CloseDate = new DateTime(2026, 3, 20), LastActivityDate = new DateTime(2026, 3, 2),
                Source = "Field Visit", FoId = arjun.Id,
                ContactName = "Mrs. Kavita Sharma", ContactDesignation = "Principal",
                ContactPhone = "+91 98201 12345", ContactEmail = "principal@dpsandheri.edu.in",
                Notes = "Principal very interested in AI Video module. Budget confirmed at 4.8L. Demo went well."
            },
            new Lead
            {
                School = "Ryan International Borivali", Board = "ICSE", City = "Mumbai", State = "Maharashtra",
                Students = 2100, Type = "Private", Stage = LeadStage.ProposalSent, Score = 74, Value = 720000,
                CloseDate = new DateTime(2026, 3, 30), LastActivityDate = new DateTime(2026, 2, 28),
                Source = "Referral", FoId = arjun.Id,
                ContactName = "Mr. Thomas George", ContactDesignation = "Director",
                ContactPhone = "+91 98202 23456", ContactEmail = "director@ryanborivali.edu.in",
                Notes = "Large school. Director is the key decision maker. Interested in full suite."
            },
            new Lead
            {
                School = "Orchid International School", Board = "IB", City = "Pune", State = "Maharashtra",
                Students = 850, Type = "Private", Stage = LeadStage.Qualified, Score = 58, Value = 320000,
                CloseDate = new DateTime(2026, 4, 15), LastActivityDate = new DateTime(2026, 2, 18),
                Source = "Website", FoId = sunita.Id,
                ContactName = "Ms. Pooja Nair", ContactDesignation = "Academic Coordinator",
                ContactPhone = "+91 98203 34567", ContactEmail = "academic@orchidpune.edu.in",
                Notes = "Interested but budget not confirmed. IB board — curriculum module fit is critical."
            },
            new Lead
            {
                School = "Shri Ram Global School", Board = "CBSE", City = "Mumbai", State = "Maharashtra",
                Students = 1800, Type = "Private", Stage = LeadStage.NewLead, Score = 35, Value = 560000,
                CloseDate = new DateTime(2026, 5, 1), LastActivityDate = new DateTime(2026, 2, 25),
                Source = "Field Visit", FoId = arjun.Id,
                ContactName = "Mr. Alok Verma", ContactDesignation = "Principal",
                ContactPhone = "+91 98204 45678", ContactEmail = "principal@shriramglobal.edu.in",
                Notes = "Just added. Initial conversation via cold visit."
            },
            new Lead
            {
                School = "Podar International School", Board = "CBSE", City = "Mumbai", State = "Maharashtra",
                Students = 3200, Type = "Private", Stage = LeadStage.Won, Score = 95, Value = 1200000,
                CloseDate = new DateTime(2026, 2, 15), LastActivityDate = new DateTime(2026, 2, 15),
                Source = "Referral", FoId = sunita.Id,
                ContactName = "Mr. Rahul Podar", ContactDesignation = "Chairman",
                ContactPhone = "+91 98205 56789", ContactEmail = "chairman@podarinternational.edu.in",
                Notes = "Closed! Full suite including AI Voice and ERP. Champion deal of the month."
            },
            new Lead
            {
                School = "Vibgyor High Thane", Board = "CBSE", City = "Thane", State = "Maharashtra",
                Students = 1400, Type = "Private", Stage = LeadStage.Qualified, Score = 47, Value = 420000,
                CloseDate = new DateTime(2026, 4, 1), LastActivityDate = new DateTime(2026, 2, 10),
                Source = "Field Visit", FoId = vikram.Id,
                ContactName = "Ms. Deepa Krishnan", ContactDesignation = "Principal",
                ContactPhone = "+91 98206 67890", ContactEmail = "principal@vibgyorthane.edu.in",
                Notes = "Lead stagnant for 20 days. Need to schedule follow-up urgently."
            },
            new Lead
            {
                School = "Euro Kids Malad", Board = "State Board", City = "Mumbai", State = "Maharashtra",
                Students = 480, Type = "Franchise", Stage = LeadStage.Contacted, Score = 28, Value = 180000,
                CloseDate = new DateTime(2026, 4, 30), LastActivityDate = new DateTime(2026, 2, 20),
                Source = "Cold Call", FoId = vikram.Id,
                ContactName = "Mrs. Reena Joshi", ContactDesignation = "Centre Head",
                ContactPhone = "+91 98207 78901", ContactEmail = "malad@eurokids.in",
                Notes = "Small franchise. Limited budget. Exploring Homework module only."
            },
            new Lead
            {
                School = "Campion School", Board = "ICSE", City = "Mumbai", State = "Maharashtra",
                Students = 2600, Type = "Private", Stage = LeadStage.Negotiation, Score = 78, Value = 890000,
                CloseDate = new DateTime(2026, 3, 15), LastActivityDate = new DateTime(2026, 3, 1),
                Source = "Referral", FoId = arjun.Id,
                ContactName = "Fr. Sebastian D'Cruz", ContactDesignation = "Principal",
                ContactPhone = "+91 98208 89012", ContactEmail = "principal@campionmumbai.edu.in",
                Notes = "In final negotiation. Requesting 15% discount — needs ZH approval."
            },
        };
        context.Leads.AddRange(leads);
        await context.SaveChangesAsync();

        // Activities
        var activities = new[]
        {
            new Activity { FoId = arjun.Id, LeadId = leads[0].Id, Type = ActivityType.Visit,    Date = new DateTime(2026, 3, 2),  Outcome = ActivityOutcome.Positive, Notes = "Conducted AI demo with principal and IT head.", GpsVerified = true },
            new Activity { FoId = arjun.Id, LeadId = leads[0].Id, Type = ActivityType.Call,     Date = new DateTime(2026, 2, 22), Outcome = ActivityOutcome.Positive, Notes = "Confirmed demo appointment for March 2nd." },
            new Activity { FoId = arjun.Id, LeadId = leads[0].Id, Type = ActivityType.Visit,    Date = new DateTime(2026, 2, 15), Outcome = ActivityOutcome.Neutral,  Notes = "Initial discovery visit. Met vice-principal.", GpsVerified = true },
            new Activity { FoId = arjun.Id, LeadId = leads[1].Id, Type = ActivityType.Proposal, Date = new DateTime(2026, 2, 28), Outcome = ActivityOutcome.Pending,  Notes = "Sent formal proposal with 8% discount." },
            new Activity { FoId = arjun.Id, LeadId = leads[1].Id, Type = ActivityType.Demo,     Date = new DateTime(2026, 2, 20), Outcome = ActivityOutcome.Positive, Notes = "Full platform demo. Director impressed with ERP module.", GpsVerified = true },
            new Activity { FoId = sunita.Id, LeadId = leads[2].Id, Type = ActivityType.Call,    Date = new DateTime(2026, 2, 18), Outcome = ActivityOutcome.Neutral,  Notes = "Follow-up call. Budget discussion." },
            new Activity { FoId = sunita.Id, LeadId = leads[2].Id, Type = ActivityType.Visit,   Date = new DateTime(2026, 2, 5),  Outcome = ActivityOutcome.Positive, Notes = "Discovery visit. Good fit for curriculum module.", GpsVerified = true },
            new Activity { FoId = arjun.Id, LeadId = leads[3].Id, Type = ActivityType.Visit,    Date = new DateTime(2026, 2, 25), Outcome = ActivityOutcome.Neutral,  Notes = "Cold visit. Scheduled callback.", GpsVerified = true },
            new Activity { FoId = sunita.Id, LeadId = leads[4].Id, Type = ActivityType.Contract,Date = new DateTime(2026, 2, 15), Outcome = ActivityOutcome.Positive, Notes = "Contract signed. Onboarding March 10th." },
            new Activity { FoId = vikram.Id, LeadId = leads[5].Id, Type = ActivityType.Visit,   Date = new DateTime(2026, 2, 10), Outcome = ActivityOutcome.Neutral,  Notes = "Principal busy with board exams.", GpsVerified = true },
            new Activity { FoId = vikram.Id, LeadId = leads[6].Id, Type = ActivityType.Call,    Date = new DateTime(2026, 2, 20), Outcome = ActivityOutcome.Neutral,  Notes = "Franchise owner approval awaited." },
            new Activity { FoId = arjun.Id, LeadId = leads[7].Id, Type = ActivityType.FollowUp, Date = new DateTime(2026, 3, 1),  Outcome = ActivityOutcome.Positive, Notes = "Price negotiation call. Submitted deal for ZH approval." },
            new Activity { FoId = arjun.Id, LeadId = leads[7].Id, Type = ActivityType.Demo,     Date = new DateTime(2026, 2, 18), Outcome = ActivityOutcome.Positive, Notes = "Full platform demo. All 7 modules shown.", GpsVerified = true },
        };
        context.Activities.AddRange(activities);

        // Deals
        var deals = new[]
        {
            new Deal
            {
                LeadId = leads[7].Id, FoId = arjun.Id, ContractValue = 890000, Discount = 15, FinalValue = 756500,
                PaymentTerms = "50% upfront, 50% post-go-live", Duration = "3 years",
                Modules = "[\"AI Voice\",\"Curriculum\",\"AI Videos\",\"ERP\",\"Homework\"]",
                Notes = "Principal motivated to close before academic year end.",
                ApprovalStatus = ApprovalStatus.PendingZH, SubmittedAt = new DateTime(2026, 3, 1)
            },
            new Deal
            {
                LeadId = leads[1].Id, FoId = arjun.Id, ContractValue = 720000, Discount = 8, FinalValue = 662400,
                PaymentTerms = "100% upfront", Duration = "2 years",
                Modules = "[\"AI Voice\",\"Curriculum\",\"AI Videos\",\"ERP\"]",
                Notes = "Director wants full suite. 8% discount within FO authority.",
                ApprovalStatus = ApprovalStatus.SelfApproved, SubmittedAt = new DateTime(2026, 2, 28)
            },
            new Deal
            {
                LeadId = leads[4].Id, FoId = sunita.Id, ContractValue = 1200000, Discount = 5, FinalValue = 1140000,
                PaymentTerms = "100% upfront", Duration = "5 years",
                Modules = "[\"AI Voice\",\"Curriculum\",\"AI Videos\",\"Lab Simulator\",\"ERP\",\"Homework\",\"Exam\"]",
                Notes = "Full suite deal. 5-year contract. Biggest win this quarter.",
                ApprovalStatus = ApprovalStatus.Approved, SubmittedAt = new DateTime(2026, 2, 10),
                ApproverId = users[3].Id // Priya Singh (ZH)
            },
        };
        context.Deals.AddRange(deals);

        // Notifications for Arjun (FO)
        var notifications = new[]
        {
            new Notification { UserId = arjun.Id, Type = NotificationType.Urgent,   Title = "Overdue Follow-up",         Body = "Vibgyor High Thane — no activity for 20 days. Schedule call now." },
            new Notification { UserId = arjun.Id, Type = NotificationType.Reminder, Title = "Demo Scheduled Today 3 PM", Body = "Campion School — prepare full platform demo materials." },
            new Notification { UserId = arjun.Id, Type = NotificationType.Success,  Title = "Deal Approved",             Body = "Ryan International Borivali — ZH approved 8% discount deal." },
            new Notification { UserId = arjun.Id, Type = NotificationType.Info,     Title = "Zone Announcement",         Body = "Monthly target updated: 20L per FO for March." },
            new Notification { UserId = arjun.Id, Type = NotificationType.Warning,  Title = "Stage Stagnation Alert",    Body = "Euro Kids Malad — stuck in Contacted for 13 days." },
        };
        context.Notifications.AddRange(notifications);

        // Tasks for Arjun
        var today = DateTime.UtcNow.Date;
        var tasks = new[]
        {
            new TaskItem { UserId = arjun.Id, ScheduledTime = today.AddHours(9),    Type = ActivityType.Call,     School = "Shri Ram Global School",        IsDone = true,  LeadId = leads[3].Id },
            new TaskItem { UserId = arjun.Id, ScheduledTime = today.AddHours(11),   Type = ActivityType.Visit,    School = "DPS Andheri",                   IsDone = true,  LeadId = leads[0].Id },
            new TaskItem { UserId = arjun.Id, ScheduledTime = today.AddHours(15),   Type = ActivityType.Demo,     School = "Campion School",                IsDone = false, LeadId = leads[7].Id },
            new TaskItem { UserId = arjun.Id, ScheduledTime = today.AddHours(17.5), Type = ActivityType.FollowUp, School = "Ryan International Borivali",   IsDone = false, LeadId = leads[1].Id },
        };
        context.Tasks.AddRange(tasks);

        await context.SaveChangesAsync();
    }
}
