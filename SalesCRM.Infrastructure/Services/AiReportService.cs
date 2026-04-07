using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class AiReportService : IAiReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IGeminiService _gemini;
    private readonly ILogger<AiReportService> _logger;

    // Prompt templates (loaded once)
    private static string? _foSystemPrompt;
    private static string? _foWeeklyPrompt;
    private static string? _foMonthlyPrompt;
    private static string? _mgmtSystemPrompt;

    public AiReportService(IUnitOfWork uow, IGeminiService gemini, ILogger<AiReportService> logger)
    {
        _uow = uow;
        _gemini = gemini;
        _logger = logger;
    }

    // ─── Prompt Loading ─────────────────────────────────────────────
    private static string FindAiReportsDir()
    {
        // Walk up from both AppContext.BaseDirectory and CWD to find AI_Reports
        var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var start in candidates)
        {
            var dir = start;
            for (int i = 0; i < 8; i++)
            {
                var check = Path.Combine(dir, "AI_Reports");
                if (Directory.Exists(check)) return check;
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == null || parent == dir) break;
                dir = parent;
            }
        }
        return "";
    }

    private static string GetFoSystemPrompt()
    {
        if (_foSystemPrompt != null) return _foSystemPrompt;
        var dir = FindAiReportsDir();
        var path = Path.Combine(dir, "FO_DAILY_REPORT_AGENT.md");
        _foSystemPrompt = File.Exists(path) ? ExtractSystemPrompt(File.ReadAllText(path)) : GetDefaultFoPrompt();
        return _foSystemPrompt;
    }

    private static string GetFoWeeklyPrompt()
    {
        if (_foWeeklyPrompt != null) return _foWeeklyPrompt;
        var dir = FindAiReportsDir();
        var path = Path.Combine(dir, "FO_WEEKLY_REPORT_AGENT.md");
        _foWeeklyPrompt = File.Exists(path) ? ExtractSystemPrompt(File.ReadAllText(path)) : GetDefaultFoPrompt();
        return _foWeeklyPrompt;
    }

    private static string GetFoMonthlyPrompt()
    {
        if (_foMonthlyPrompt != null) return _foMonthlyPrompt;
        var dir = FindAiReportsDir();
        var path = Path.Combine(dir, "FO_MONTHLY_REPORT_AGENT.md");
        _foMonthlyPrompt = File.Exists(path) ? ExtractSystemPrompt(File.ReadAllText(path)) : GetDefaultFoPrompt();
        return _foMonthlyPrompt;
    }

    private static string GetMgmtSystemPrompt()
    {
        if (_mgmtSystemPrompt != null) return _mgmtSystemPrompt;
        var dir = FindAiReportsDir();
        var path = Path.Combine(dir, "MANAGEMENT_REPORT_AGENT.md");
        _mgmtSystemPrompt = File.Exists(path) ? ExtractSystemPrompt(File.ReadAllText(path)) : GetDefaultMgmtPrompt();
        return _mgmtSystemPrompt;
    }

    private static string ExtractSystemPrompt(string mdContent)
    {
        // Extract content between ```system prompt``` code blocks
        var startMarker = "```\nYou are";
        var endMarker = "```\n\n## Input";
        var start = mdContent.IndexOf(startMarker);
        var end = mdContent.IndexOf(endMarker);
        if (start >= 0 && end > start)
            return mdContent.Substring(start + 4, end - start - 4).Trim();
        // Fallback: extract first ``` block
        start = mdContent.IndexOf("```\n");
        if (start >= 0)
        {
            end = mdContent.IndexOf("```", start + 4);
            if (end > start)
                return mdContent.Substring(start + 4, end - start - 4).Trim();
        }
        return mdContent;
    }

    private static string GetDefaultFoPrompt() =>
        "You are a strict Field Officer Performance Auditor. Analyze the FO's daily data and return a JSON report with sections, redFlags, aiInsights, overallScore, overallRating, and scoreBreakdown. Rate each section as Good/Average/Poor. Be direct and specific.";

    private static string GetDefaultMgmtPrompt() =>
        "You are a Sales Team Performance Strategist. Analyze the team's weekly data and return a JSON report with sections, teamRanking, disciplineReport, redFlags, aiInsights, overallHealthScore, overallRating, and trendVsPreviousPeriod. Be strategic and data-driven.";

    // ─── FO Daily Data Collection ───────────────────────────────────
    public async Task<string> CollectFoDailyDataAsync(int foId, DateTime date)
    {
        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var nextDay = dateUtc.AddDays(1);

        var fo = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == foId);
        if (fo == null) return "{}";

        // Tracking session
        var session = await _uow.TrackingSessions.Query()
            .FirstOrDefaultAsync(s => s.UserId == foId && s.SessionDate >= dateUtc && s.SessionDate < nextDay);

        // School visit logs
        var visitLogs = await _uow.SchoolVisitLogs.Query()
            .Include(v => v.School)
            .Where(v => v.UserId == foId && v.VisitDate >= dateUtc && v.VisitDate < nextDay)
            .OrderBy(v => v.EnteredAt)
            .ToListAsync();

        // School assignments
        var assignments = await _uow.SchoolAssignments.Query()
            .Include(a => a.School)
            .Where(a => a.UserId == foId && a.AssignmentDate >= dateUtc && a.AssignmentDate < nextDay)
            .OrderBy(a => a.VisitOrder)
            .ToListAsync();

        // Visit reports
        var visitReports = await _uow.VisitReports.Query()
            .Include(v => v.School).Include(v => v.PersonMet)
            .Where(v => v.UserId == foId && v.CreatedAt >= dateUtc && v.CreatedAt < nextDay)
            .ToListAsync();

        // Activities
        var activities = await _uow.Activities.Query()
            .Include(a => a.Lead)
            .Where(a => a.FoId == foId && a.Date >= dateUtc && a.Date < nextDay)
            .ToListAsync();

        // Daily allowance
        var allowance = await _uow.DailyAllowances.Query()
            .FirstOrDefaultAsync(a => a.UserId == foId && a.AllowanceDate >= dateUtc && a.AllowanceDate < nextDay);

        // Route plan
        var routePlan = await _uow.DailyRoutePlans.Query()
            .FirstOrDefaultAsync(r => r.UserId == foId && r.PlanDate >= dateUtc && r.PlanDate < nextDay);

        // Leads with recent activity
        var foLeads = await _uow.Leads.Query()
            .Where(l => l.FoId == foId)
            .ToListAsync();
        var newLeads = foLeads.Where(l => l.CreatedAt >= dateUtc && l.CreatedAt < nextDay).ToList();
        var hotLeads = foLeads.Where(l => l.Score >= 70 && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();

        // Deals
        var deals = await _uow.Deals.Query()
            .Where(d => d.FoId == foId && d.CreatedAt >= dateUtc && d.CreatedAt < nextDay)
            .ToListAsync();

        // Targets
        var target = await _uow.TargetAssignments.Query()
            .Where(t => t.AssignedToId == foId && t.StartDate <= dateUtc && t.EndDate >= dateUtc)
            .FirstOrDefaultAsync();

        // Follow-up compliance: VisitReports from past with NextActionDate <= today
        var overdueFollowUps = await _uow.VisitReports.Query()
            .Include(v => v.School)
            .Where(v => v.UserId == foId && v.NextActionDate.HasValue && v.NextActionDate.Value <= dateUtc
                         && v.NextAction != NextActionType.None)
            .ToListAsync();

        // Fraud aggregation from location pings
        int totalPings = 0, invalidPings = 0, mockedPings = 0, filteredPings = 0, speedViolations = 0;
        if (session != null)
        {
            var pingStats = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Invalid = g.Count(p => !p.IsValid),
                    Mocked = g.Count(p => p.IsMocked),
                    Filtered = g.Count(p => p.IsFiltered),
                    SpeedViolations = g.Count(p => p.SpeedKmh > 150)
                })
                .FirstOrDefaultAsync();

            if (pingStats != null)
            {
                totalPings = pingStats.Total;
                invalidPings = pingStats.Invalid;
                mockedPings = pingStats.Mocked;
                filteredPings = pingStats.Filtered;
                speedViolations = pingStats.SpeedViolations;
            }
        }

        // ─── Compute pre-aggregated metrics ───
        var totalSessionMinutes = session?.StartedAt != null && session?.EndedAt != null
            ? (int)(session.EndedAt.Value - session.StartedAt.Value).TotalMinutes : 0;
        var inSchoolMinutes = (int)visitLogs.Where(v => v.DurationMinutes.HasValue).Sum(v => (double)v.DurationMinutes!.Value);
        var productivePercent = totalSessionMinutes > 0 ? Math.Round((double)(inSchoolMinutes) / totalSessionMinutes * 100, 1) : 0;

        // Assignment compliance
        var assignedIds = assignments.Select(a => a.SchoolId).ToHashSet();
        var visitedIds = visitLogs.Select(v => v.SchoolId).ToHashSet();
        var missedSchools = assignments.Where(a => !visitedIds.Contains(a.SchoolId)).Select(a => a.School?.Name ?? "Unknown").ToList();
        var unplannedVisits = visitLogs.Where(v => !assignedIds.Contains(v.SchoolId)).Select(v => v.School?.Name ?? "Unknown").ToList();
        var compliancePercent = assignments.Count > 0 ? Math.Round((double)assignments.Count(a => a.IsVisited) / assignments.Count * 100, 1) : 100;

        // Visit quality
        var visitQuality = visitLogs.Select(v =>
        {
            var report = visitReports.FirstOrDefault(r => r.SchoolId == v.SchoolId);
            var hasReport = report != null;
            var hasPhotos = report?.Photos != null && report.Photos != "[]" && report.Photos.Length > 5;
            var photoCount = 0;
            if (hasPhotos) try { photoCount = JsonSerializer.Deserialize<string[]>(report!.Photos!)?.Length ?? 0; } catch { }
            var personMet = report?.PersonMetId != null;
            var outcomeRecorded = !string.IsNullOrEmpty(report?.Outcome);
            var score = (v.DurationMinutes >= 15 ? 30 : 0) + (hasReport ? 25 : 0) + (hasPhotos ? 15 : 0) + (personMet ? 15 : 0) + (outcomeRecorded ? 15 : 0);
            var flag = v.DurationMinutes < 5 ? "DRIVE_BY_VISIT" : !hasReport ? "NO_REPORT_FILED" : !hasPhotos ? "MISSING_PHOTOS" : score >= 85 ? "HIGH_QUALITY" : "GOOD_VISIT";
            return new { school = v.School?.Name ?? "", durationMinutes = (int)(v.DurationMinutes ?? 0), hasVisitReport = hasReport, hasPhotos, photoCount, personMet, outcomeRecorded, qualityScore = score, flag };
        }).ToList();

        // Inter-school gaps
        var gaps = new List<object>();
        for (int i = 0; i < visitLogs.Count - 1; i++)
        {
            var curr = visitLogs[i];
            var next = visitLogs[i + 1];
            if (curr.ExitedAt == null) continue;
            var gapMin = (int)(next.EnteredAt - curr.ExitedAt.Value).TotalMinutes;
            var dist = HaversineKm(curr.School?.Latitude ?? 0, curr.School?.Longitude ?? 0, next.School?.Latitude ?? 0, next.School?.Longitude ?? 0);
            var expectedMin = (int)(dist / 25.0 * 60);
            var excess = Math.Max(0, gapMin - expectedMin);
            gaps.Add(new
            {
                fromSchool = curr.School?.Name ?? "",
                exitTime = curr.ExitedAt.Value.ToString("HH:mm"),
                toSchool = next.School?.Name ?? "",
                entryTime = next.EnteredAt.ToString("HH:mm"),
                gapMinutes = gapMin,
                straightLineDistanceKm = Math.Round(dist, 1),
                expectedTravelMinutes = expectedMin,
                excessMinutes = excess,
                flag = excess > expectedMin ? "EXCESSIVE_GAP" : "OK"
            });
        }

        // Punctuality
        var ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var startIst = session?.StartedAt != null ? TimeZoneInfo.ConvertTimeFromUtc(session.StartedAt.Value, ist) : (DateTime?)null;
        var endIst = session?.EndedAt != null ? TimeZoneInfo.ConvertTimeFromUtc(session.EndedAt.Value, ist) : (DateTime?)null;
        var expectedStart = new TimeSpan(9, 0, 0);
        var expectedEnd = new TimeSpan(18, 0, 0);
        var lateBy = startIst != null ? Math.Max(0, (int)(startIst.Value.TimeOfDay - expectedStart).TotalMinutes) : 0;
        var earlyBy = endIst != null ? Math.Max(0, (int)(expectedEnd - endIst.Value.TimeOfDay).TotalMinutes) : 0;

        // Productivity score
        var avgVisitQuality = visitQuality.Any() ? (int)visitQuality.Average(v => v.qualityScore) : 0;
        var punctualityScore = Math.Max(0, 100 - (lateBy + earlyBy) * 100 / 540);
        var followUpDue = overdueFollowUps.Count;
        var followUpDone = overdueFollowUps.Count(f => activities.Any(a => a.Lead?.School == f.School?.Name));
        var followUpPercent = followUpDue > 0 ? Math.Round((double)followUpDone / followUpDue * 100, 1) : 100;
        var fraudPenalty = (mockedPings > 0 ? -15 : 0) + (speedViolations > 2 ? -5 : 0);
        var overallScore = (int)(productivePercent * 0.25 + avgVisitQuality * 0.25 + compliancePercent * 0.20 + punctualityScore * 0.15 + followUpPercent * 0.15) + fraudPenalty;
        overallScore = Math.Clamp(overallScore, 0, 100);
        var rating = overallScore >= 70 ? "Good" : overallScore >= 50 ? "Average" : "Poor";

        var data = new
        {
            reportDate = date.ToString("yyyy-MM-dd"),
            fo = new { id = fo.Id, name = fo.Name ?? "", zoneName = fo.Zone?.Name ?? "", regionName = fo.Region?.Name ?? "" },
            attendance = new
            {
                sessionStartedAt = session?.StartedAt,
                sessionEndedAt = session?.EndedAt,
                totalHoursWorked = Math.Round(totalSessionMinutes / 60.0, 2),
                sessionStatus = session?.Status.ToString() ?? "NoSession"
            },
            punctuality = new
            {
                expectedStartTime = "09:00",
                actualStartTime = startIst?.ToString("HH:mm") ?? "N/A",
                lateByMinutes = lateBy,
                expectedEndTime = "18:00",
                actualEndTime = endIst?.ToString("HH:mm") ?? "N/A",
                earlyByMinutes = earlyBy,
                effectiveWorkHours = Math.Round(totalSessionMinutes / 60.0, 2),
                expectedWorkHours = 9.0,
                workHoursDeficit = Math.Round(9.0 - totalSessionMinutes / 60.0, 2),
                flag = lateBy > 15 && earlyBy > 30 ? "LATE_START_EARLY_END" : lateBy > 15 ? "LATE_START" : earlyBy > 30 ? "EARLY_END" : "OK"
            },
            travel = new
            {
                rawDistanceKm = session?.RawDistanceKm ?? 0,
                filteredDistanceKm = session?.FilteredDistanceKm ?? 0,
                reconstructedDistanceKm = session?.ReconstructedDistanceKm ?? 0,
                allowanceAmount = allowance?.GrossAllowance ?? session?.AllowanceAmount ?? 0,
                allowanceRatePerKm = session?.AllowanceRatePerKm ?? 10
            },
            routePlan = new
            {
                hasRoutePlan = routePlan != null,
                estimatedDistanceKm = routePlan?.TotalEstimatedDistanceKm ?? 0,
                actualDistanceKm = routePlan?.TotalActualDistanceKm ?? session?.FilteredDistanceKm ?? 0,
                plannedStops = routePlan?.Stops != null ? TryParseStops(routePlan.Stops) : Array.Empty<string>(),
                status = routePlan?.Status.ToString() ?? "None"
            },
            timeBreakdown = new
            {
                totalSessionMinutes,
                inSchoolMinutes,
                travellingMinutes = totalSessionMinutes - inSchoolMinutes, // simplified
                idleMinutes = 0, // would need cluster analysis for precise idle
                unaccountedMinutes = 0,
                productivePercent
            },
            schoolVisits = visitLogs.Select(v => new
            {
                schoolName = v.School?.Name ?? "",
                city = v.School?.City ?? "",
                enteredAt = v.EnteredAt,
                exitedAt = v.ExitedAt,
                durationMinutes = (int)(v.DurationMinutes ?? 0),
                wasAssigned = assignedIds.Contains(v.SchoolId),
                assignmentOrder = assignments.FirstOrDefault(a => a.SchoolId == v.SchoolId)?.VisitOrder ?? 0
            }),
            visitQuality,
            interSchoolGaps = gaps,
            routeCompliance = new
            {
                assignedSchools = assignments.Count,
                visitedAssigned = assignments.Count(a => a.IsVisited),
                missedAssigned = missedSchools,
                unplannedVisits,
                compliancePercent,
                plannedOrder = assignments.Select(a => a.School?.Name ?? "").ToList(),
                actualOrder = visitLogs.Select(v => v.School?.Name ?? "").ToList(),
                flag = compliancePercent < 60 ? "LOW_COMPLIANCE" : compliancePercent < 80 ? "MODERATE_COMPLIANCE" : "GOOD"
            },
            visitReports = visitReports.Select(r => new
            {
                schoolName = r.School?.Name ?? "",
                purpose = r.Purpose.ToString(),
                personMet = r.PersonMet != null ? $"{r.PersonMet.Name} ({r.PersonMet.Designation})" : "None",
                outcome = r.Outcome ?? "",
                remarks = r.Remarks ?? "",
                nextAction = r.NextAction.ToString(),
                nextActionDate = r.NextActionDate?.ToString("yyyy-MM-dd") ?? "",
                photoCount = 0
            }),
            activitySummary = new
            {
                visits = activities.Count(a => a.Type == ActivityType.Visit),
                demos = activities.Count(a => a.Type == ActivityType.Demo),
                calls = activities.Count(a => a.Type == ActivityType.Call),
                followUps = activities.Count(a => a.Type == ActivityType.FollowUp),
                proposals = activities.Count(a => a.Type == ActivityType.Proposal)
            },
            leads = new
            {
                newLeadsToday = newLeads.Count,
                hotLeads = hotLeads.Take(5).Select(l => new { school = l.School, stage = l.Stage.ToString(), score = l.Score, value = l.Value }),
                totalActiveLeads = foLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost),
                lostLeads = foLeads.Where(l => l.Stage == LeadStage.Lost).Select(l => new { school = l.School, value = l.Value, reason = l.LossReason ?? "Not specified" }),
                totalLost = foLeads.Count(l => l.Stage == LeadStage.Lost)
            },
            deals = new { closedToday = deals.Count, totalValueToday = deals.Sum(d => d.FinalValue) },
            targets = target != null ? new
            {
                title = target.Title ?? "",
                periodType = target.PeriodType.ToString(),
                targetAmount = target.TargetAmount,
                achievedAmount = target.AchievedAmount,
                percentComplete = target.TargetAmount > 0 ? Math.Round((double)target.AchievedAmount / (double)target.TargetAmount * 100, 1) : 0,
                targetSchools = target.NumberOfSchools,
                achievedSchools = target.AchievedSchools,
                daysRemaining = (target.EndDate - dateUtc).Days
            } : (object?)null,
            fraudAnalysis = new
            {
                fraudScore = session?.FraudScore ?? 0,
                isSuspicious = session?.IsSuspicious ?? false,
                totalPings, invalidPings, mockedLocationPings = mockedPings, filteredPings, speedViolations,
                teleportEvents = 0,
                rawVsFilteredDistanceKm = new
                {
                    raw = session?.RawDistanceKm ?? 0,
                    filtered = session?.FilteredDistanceKm ?? 0,
                    difference = (session?.RawDistanceKm ?? 0) - (session?.FilteredDistanceKm ?? 0),
                    inflationPercent = session?.FilteredDistanceKm > 0
                        ? Math.Round((double)((session.RawDistanceKm - session.FilteredDistanceKm) / session.FilteredDistanceKm * 100), 1) : 0
                },
                flags = new List<string>()
            },
            followUpCompliance = new
            {
                dueToday = followUpDue,
                completedToday = followUpDone,
                overdue = overdueFollowUps.Where(f => !activities.Any(a => a.Lead?.School == f.School?.Name))
                    .Select(f => new { school = f.School?.Name ?? "", action = f.NextAction.ToString(), dueDate = f.NextActionDate?.ToString("yyyy-MM-dd") ?? "", overdueDays = (dateUtc - (f.NextActionDate ?? dateUtc)).Days }),
                compliancePercent = followUpPercent,
                flag = followUpPercent < 50 ? "LOW_FOLLOWUP_COMPLIANCE" : "OK"
            },
            productivityScore = new
            {
                timeScore = (int)productivePercent,
                visitQualityScore = avgVisitQuality,
                complianceScore = (int)compliancePercent,
                punctualityScore,
                followUpScore = (int)followUpPercent,
                fraudPenalty,
                overallScore,
                rating
            }
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
    }

    // ─── Management Data Collection ─────────────────────────────────
    public async Task<string> CollectManagementDataAsync(int managerId, DateTime periodStart, DateTime periodEnd)
    {
        var startUtc = DateTime.SpecifyKind(periodStart.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(periodEnd.Date.AddDays(1), DateTimeKind.Utc);
        var prevStart = startUtc.AddDays(-(endUtc - startUtc).TotalDays);
        var prevEnd = startUtc;

        var manager = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == managerId);
        if (manager == null) return "{}";

        // Get FOs in scope
        var fosQuery = _uow.Users.Query().Where(u => u.Role == UserRole.FO);
        if (manager.Role == UserRole.ZH && manager.ZoneId.HasValue)
            fosQuery = fosQuery.Where(u => u.ZoneId == manager.ZoneId);
        else if (manager.Role == UserRole.RH && manager.RegionId.HasValue)
            fosQuery = fosQuery.Where(u => u.RegionId == manager.RegionId);
        // SH and SCA see all FOs

        var fos = await fosQuery.ToListAsync();
        var foIds = fos.Select(f => f.Id).ToList();

        // Current period data
        var sessions = await _uow.TrackingSessions.Query()
            .Where(s => foIds.Contains(s.UserId) && s.SessionDate >= startUtc && s.SessionDate < endUtc)
            .ToListAsync();
        var visitLogs = await _uow.SchoolVisitLogs.Query().Include(v => v.School)
            .Where(v => foIds.Contains(v.UserId) && v.VisitDate >= startUtc && v.VisitDate < endUtc)
            .ToListAsync();
        var demos = await _uow.DemoAssignments.Query()
            .Where(d => foIds.Contains(d.AssignedToId) && d.ScheduledDate >= startUtc && d.ScheduledDate < endUtc)
            .ToListAsync();
        var dealsCurr = await _uow.Deals.Query()
            .Where(d => foIds.Contains(d.FoId) && d.CreatedAt >= startUtc && d.CreatedAt < endUtc)
            .ToListAsync();
        var assignments = await _uow.SchoolAssignments.Query()
            .Where(a => foIds.Contains(a.UserId) && a.AssignmentDate >= startUtc && a.AssignmentDate < endUtc)
            .ToListAsync();
        var targets = await _uow.TargetAssignments.Query()
            .Where(t => foIds.Contains(t.AssignedToId) && t.StartDate <= endUtc && t.EndDate >= startUtc)
            .ToListAsync();

        // Previous period AI reports for scores
        var prevReports = await _uow.AiReports.Query()
            .Where(r => foIds.Contains(r.UserId) && r.ReportType == AiReportType.FoDaily
                         && r.ReportDate >= prevStart && r.ReportDate < prevEnd && r.Status == AiReportStatus.Completed)
            .ToListAsync();

        // Current period AI reports for scores
        var currReports = await _uow.AiReports.Query()
            .Where(r => foIds.Contains(r.UserId) && r.ReportType == AiReportType.FoDaily
                         && r.ReportDate >= startUtc && r.ReportDate < endUtc && r.Status == AiReportStatus.Completed)
            .ToListAsync();

        // Total schools in territory
        var totalSchools = await _uow.Schools.Query().Where(s => s.IsActive).CountAsync();
        var visitedSchoolIds = visitLogs.Select(v => v.SchoolId).Distinct().Count();

        // Pipeline
        var leads = await _uow.Leads.Query().Where(l => foIds.Contains(l.FoId)).ToListAsync();

        var teamPerformance = fos.Select(fo =>
        {
            var foSessions = sessions.Where(s => s.UserId == fo.Id).ToList();
            var foVisits = visitLogs.Where(v => v.UserId == fo.Id).ToList();
            var foDemos = demos.Where(d => d.AssignedToId == fo.Id).ToList();
            var foDeals = dealsCurr.Where(d => d.FoId == fo.Id).ToList();
            var foAssignments = assignments.Where(a => a.UserId == fo.Id).ToList();
            var foTarget = targets.FirstOrDefault(t => t.AssignedToId == fo.Id);
            var foReports = currReports.Where(r => r.UserId == fo.Id).ToList();
            var avgScore = foReports.Any() ? (int)foReports.Average(r => r.OverallScore) : 0;

            return new
            {
                foId = fo.Id,
                foName = fo.Name ?? "",
                totalVisits = foVisits.Count,
                totalDemos = foDemos.Count,
                totalDeals = foDeals.Count,
                totalRevenue = foDeals.Sum(d => d.FinalValue),
                totalDistanceKm = foSessions.Sum(s => s.FilteredDistanceKm),
                totalAllowance = foSessions.Sum(s => s.AllowanceAmount),
                daysWorked = foSessions.Count(s => s.Status == TrackingSessionStatus.Ended),
                avgDailyScore = avgScore,
                avgFraudScore = foSessions.Any() ? (int)foSessions.Average(s => s.FraudScore) : 0,
                lateStartCount = 0, // simplified
                missedAssignmentCount = foAssignments.Count(a => !a.IsVisited),
                targetCompletionPercent = foTarget != null && foTarget.TargetAmount > 0
                    ? Math.Round((double)foTarget.AchievedAmount / (double)foTarget.TargetAmount * 100, 1) : 0
            };
        }).OrderByDescending(f => f.avgDailyScore).ToList();

        // Previous period summary
        var prevSessions = await _uow.TrackingSessions.Query()
            .Where(s => foIds.Contains(s.UserId) && s.SessionDate >= prevStart && s.SessionDate < prevEnd)
            .ToListAsync();
        var prevDeals = await _uow.Deals.Query()
            .Where(d => foIds.Contains(d.FoId) && d.CreatedAt >= prevStart && d.CreatedAt < prevEnd)
            .ToListAsync();

        var data = new
        {
            reportPeriod = new { startDate = periodStart.ToString("yyyy-MM-dd"), endDate = periodEnd.ToString("yyyy-MM-dd"), periodLabel = $"{periodStart:d MMM} - {periodEnd:d MMM yyyy}" },
            scope = new { role = manager.Role.ToString(), managerName = manager.Name ?? "", zoneName = manager.Zone?.Name ?? "", regionName = manager.Region?.Name ?? "", totalFOs = fos.Count },
            teamPerformance,
            previousPeriod = new
            {
                periodLabel = $"{prevStart:d MMM} - {prevEnd.AddDays(-1):d MMM yyyy}",
                teamTotalRevenue = prevDeals.Sum(d => d.FinalValue),
                teamAvgScore = prevReports.Any() ? (int)prevReports.Average(r => r.OverallScore) : 0
            },
            territoryCoverage = new { totalSchoolsInTerritory = totalSchools, schoolsVisitedThisPeriod = visitedSchoolIds, coveragePercent = totalSchools > 0 ? Math.Round((double)visitedSchoolIds / totalSchools * 100, 1) : 0 },
            pipeline = new
            {
                leadsByStage = leads.GroupBy(l => l.Stage.ToString()).Select(g => new { stage = g.Key, count = g.Count(), value = g.Sum(l => l.Value) }),
                stuckLeads = leads.Where(l => (DateTime.UtcNow - l.UpdatedAt).TotalDays > 10 && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
                    .Take(10).Select(l => new { school = l.School, stage = l.Stage.ToString(), daysInStage = (int)(DateTime.UtcNow - l.UpdatedAt).TotalDays, foName = fos.FirstOrDefault(f => f.Id == l.FoId)?.Name ?? "", value = l.Value })
            },
            revenue = new
            {
                totalDeals = dealsCurr.Count,
                totalRevenue = dealsCurr.Sum(d => d.FinalValue),
                avgDealSize = dealsCurr.Any() ? dealsCurr.Average(d => d.FinalValue) : 0,
                previousPeriodRevenue = prevDeals.Sum(d => d.FinalValue)
            },
            demoEffectiveness = new
            {
                totalDemos = demos.Count,
                demosCompleted = demos.Count(d => d.Status == DemoStatus.Completed),
                conversionRate = demos.Count > 0 ? Math.Round((double)demos.Count(d => d.Outcome == DemoOutcome.Successful) / demos.Count * 100, 1) : 0
            },
            bottomPerformers = teamPerformance.Where(f => f.avgDailyScore < 50 && f.avgDailyScore > 0)
                .Select(f => new { f.foName, f.avgDailyScore, primaryIssues = new List<string>() })
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
    }

    // ─── Report Generation ──────────────────────────────────────────
    public async Task<AiReport> GenerateFoDailyReportAsync(int foId, DateTime date)
    {
        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Idempotency check
        var existing = await _uow.AiReports.Query()
            .FirstOrDefaultAsync(r => r.UserId == foId && r.ReportType == AiReportType.FoDaily && r.ReportDate == dateUtc && r.Status == AiReportStatus.Completed);
        if (existing != null) return existing;

        var report = new AiReport
        {
            UserId = foId, ReportType = AiReportType.FoDaily,
            ReportDate = dateUtc, PeriodStart = dateUtc, PeriodEnd = dateUtc,
            Status = AiReportStatus.Pending
        };
        await _uow.AiReports.AddAsync(report);
        await _uow.SaveChangesAsync();

        try
        {
            var inputJson = await CollectFoDailyDataAsync(foId, date);
            report.InputDataJson = inputJson;
            report.Status = AiReportStatus.Generating;
            await _uow.AiReports.UpdateAsync(report);
            await _uow.SaveChangesAsync();

            var systemPrompt = GetFoSystemPrompt();
            var geminiResult = await _gemini.GenerateContentAsync(systemPrompt, inputJson);

            if (!geminiResult.Success)
            {
                report.Status = AiReportStatus.Failed;
                report.ErrorMessage = geminiResult.Error;
                await _uow.AiReports.UpdateAsync(report);
                await _uow.SaveChangesAsync();
                return report;
            }

            report.OutputJson = geminiResult.Content;
            report.GeminiTokensUsed = geminiResult.TokensUsed;
            report.GeneratedAt = DateTime.UtcNow;

            // Extract score and rating from output
            try
            {
                using var doc = JsonDocument.Parse(geminiResult.Content);
                foreach (var key in new[] { "overallScore", "score", "productivityScore" })
                    if (doc.RootElement.TryGetProperty(key, out var sp) && sp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    { report.OverallScore = sp.GetInt32(); break; }
                foreach (var key in new[] { "overallRating", "rating" })
                    if (doc.RootElement.TryGetProperty(key, out var rp) && rp.ValueKind == System.Text.Json.JsonValueKind.String)
                    { report.OverallRating = rp.GetString() ?? ""; break; }
                if (string.IsNullOrEmpty(report.OverallRating))
                    report.OverallRating = report.OverallScore >= 70 ? "Good" : report.OverallScore >= 50 ? "Average" : "Poor";
            }
            catch { }

            report.Status = AiReportStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate FO daily report for user {FoId} on {Date}", foId, date);
            report.Status = AiReportStatus.Failed;
            report.ErrorMessage = ex.Message;
        }

        await _uow.AiReports.UpdateAsync(report);
        await _uow.SaveChangesAsync();
        return report;
    }

    // ─── FO Weekly/Monthly Report Generation ─────────────────────
    public async Task<AiReport> GenerateFoPeriodReportAsync(int foId, AiReportType reportType, DateTime periodStart, DateTime periodEnd)
    {
        var startUtc = DateTime.SpecifyKind(periodStart.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(periodEnd.Date, DateTimeKind.Utc);

        var existing = await _uow.AiReports.Query()
            .FirstOrDefaultAsync(r => r.UserId == foId && r.ReportType == reportType && r.PeriodStart == startUtc && r.PeriodEnd == endUtc
                                       && (r.Status == AiReportStatus.Completed || r.Status == AiReportStatus.Generating));
        if (existing != null) return existing;

        var report = new AiReport
        {
            UserId = foId, ReportType = reportType,
            ReportDate = endUtc, PeriodStart = startUtc, PeriodEnd = endUtc,
            Status = AiReportStatus.Pending
        };
        await _uow.AiReports.AddAsync(report);
        await _uow.SaveChangesAsync();

        try
        {
            // Collect data for the period (reuse management data collector scoped to single FO)
            var inputJson = await CollectFoPeriodDataAsync(foId, periodStart, periodEnd);
            report.InputDataJson = inputJson;
            report.Status = AiReportStatus.Generating;
            await _uow.AiReports.UpdateAsync(report);
            await _uow.SaveChangesAsync();

            var systemPrompt = reportType == AiReportType.FoWeekly ? GetFoWeeklyPrompt() : GetFoMonthlyPrompt();
            var geminiResult = await _gemini.GenerateContentAsync(systemPrompt, inputJson);

            if (!geminiResult.Success)
            {
                report.Status = AiReportStatus.Failed;
                report.ErrorMessage = geminiResult.Error;
                await _uow.AiReports.UpdateAsync(report);
                await _uow.SaveChangesAsync();
                return report;
            }

            report.OutputJson = geminiResult.Content;
            report.GeminiTokensUsed = geminiResult.TokensUsed;
            report.GeneratedAt = DateTime.UtcNow;

            try
            {
                using var doc = JsonDocument.Parse(geminiResult.Content);
                foreach (var key in new[] { "overallScore", "score" })
                    if (doc.RootElement.TryGetProperty(key, out var sp))
                    {
                        if (sp.ValueKind == JsonValueKind.Number) { report.OverallScore = sp.GetInt32(); break; }
                        if (sp.ValueKind == JsonValueKind.Object && sp.TryGetProperty("current", out var cp) && cp.ValueKind == JsonValueKind.Number)
                        { report.OverallScore = cp.GetInt32(); break; }
                    }
                foreach (var key in new[] { "overallRating", "rating" })
                    if (doc.RootElement.TryGetProperty(key, out var rp) && rp.ValueKind == JsonValueKind.String)
                    { report.OverallRating = rp.GetString() ?? ""; break; }
                if (doc.RootElement.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Object
                    && scoreProp.TryGetProperty("rating", out var ratingProp) && ratingProp.ValueKind == JsonValueKind.String)
                    report.OverallRating = ratingProp.GetString() ?? report.OverallRating;
                if (string.IsNullOrEmpty(report.OverallRating))
                    report.OverallRating = report.OverallScore >= 70 ? "Good" : report.OverallScore >= 50 ? "Average" : "Poor";
            }
            catch { }

            report.Status = AiReportStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate FO {Type} report for user {FoId}", reportType, foId);
            report.Status = AiReportStatus.Failed;
            report.ErrorMessage = ex.Message;
        }

        await _uow.AiReports.UpdateAsync(report);
        await _uow.SaveChangesAsync();
        return report;
    }

    private async Task<string> CollectFoPeriodDataAsync(int foId, DateTime periodStart, DateTime periodEnd)
    {
        var startUtc = DateTime.SpecifyKind(periodStart.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(periodEnd.Date.AddDays(1), DateTimeKind.Utc);

        var fo = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == foId);
        if (fo == null) return "{}";

        var sessions = await _uow.TrackingSessions.Query()
            .Where(s => s.UserId == foId && s.SessionDate >= startUtc && s.SessionDate < endUtc)
            .ToListAsync();
        var visitLogs = await _uow.SchoolVisitLogs.Query().Include(v => v.School)
            .Where(v => v.UserId == foId && v.VisitDate >= startUtc && v.VisitDate < endUtc)
            .ToListAsync();
        var activities = await _uow.Activities.Query().Include(a => a.Lead)
            .Where(a => a.FoId == foId && a.Date >= startUtc && a.Date < endUtc)
            .ToListAsync();
        var deals = await _uow.Deals.Query()
            .Where(d => d.FoId == foId && d.CreatedAt >= startUtc && d.CreatedAt < endUtc)
            .ToListAsync();
        var foLeads = await _uow.Leads.Query().Where(l => l.FoId == foId).ToListAsync();
        var target = await _uow.TargetAssignments.Query()
            .Where(t => t.AssignedToId == foId && t.StartDate <= endUtc && t.EndDate >= startUtc)
            .FirstOrDefaultAsync();
        var assignments = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == foId && a.AssignmentDate >= startUtc && a.AssignmentDate < endUtc)
            .ToListAsync();

        var totalDays = sessions.Count(s => s.Status == TrackingSessionStatus.Ended);
        var totalHours = sessions.Where(s => s.StartedAt != null && s.EndedAt != null)
            .Sum(s => (s.EndedAt!.Value - s.StartedAt!.Value).TotalHours);
        var totalKm = sessions.Sum(s => s.FilteredDistanceKm);
        var totalAllowance = sessions.Sum(s => s.AllowanceAmount);
        var inSchoolMin = visitLogs.Sum(v => (double)(v.DurationMinutes ?? 0));
        var visitedSchoolIds = visitLogs.Select(v => v.SchoolId).Distinct().Count();
        var totalSchools = await _uow.Schools.Query().Where(s => s.IsActive).CountAsync();
        var avgFraudScore = sessions.Any() ? (int)sessions.Average(s => s.FraudScore) : 0;
        var missedAssignments = assignments.Count(a => !a.IsVisited);
        var routeCompliance = assignments.Count > 0 ? Math.Round((double)assignments.Count(a => a.IsVisited) / assignments.Count * 100, 1) : 100;

        // Daily breakdown
        var dayBreakdown = new List<object>();
        for (var d = startUtc; d < endUtc; d = d.AddDays(1))
        {
            var nextD = d.AddDays(1);
            var daySession = sessions.FirstOrDefault(s => s.SessionDate >= d && s.SessionDate < nextD);
            var dayVisits = visitLogs.Count(v => v.VisitDate >= d && v.VisitDate < nextD);
            var dayDemos = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= d && a.Date < nextD);
            var dayCalls = activities.Count(a => a.Type == ActivityType.Call && a.Date >= d && a.Date < nextD);
            var dayRevenue = deals.Where(dl => dl.CreatedAt >= d && dl.CreatedAt < nextD).Sum(dl => dl.FinalValue);
            var dayHours = daySession?.StartedAt != null && daySession?.EndedAt != null
                ? Math.Round((daySession.EndedAt.Value - daySession.StartedAt.Value).TotalHours, 1) : 0;
            dayBreakdown.Add(new { date = d.ToString("yyyy-MM-dd"), day = d.DayOfWeek.ToString()[..3], visits = dayVisits, demos = dayDemos, calls = dayCalls, hours = dayHours, revenue = dayRevenue });
        }

        var data = new
        {
            reportPeriod = new { startDate = periodStart.ToString("yyyy-MM-dd"), endDate = periodEnd.ToString("yyyy-MM-dd"), periodLabel = $"{periodStart:d MMM} - {periodEnd:d MMM yyyy}" },
            fo = new { id = fo.Id, name = fo.Name ?? "", zoneName = fo.Zone?.Name ?? "", regionName = fo.Region?.Name ?? "" },
            summary = new
            {
                daysWorked = totalDays, totalHoursWorked = Math.Round(totalHours, 1), avgHoursPerDay = totalDays > 0 ? Math.Round(totalHours / totalDays, 1) : 0,
                totalDistanceKm = Math.Round((double)totalKm, 1), totalAllowance = Math.Round((double)totalAllowance, 0),
                totalVisits = visitLogs.Count, totalDemos = activities.Count(a => a.Type == ActivityType.Demo),
                totalCalls = activities.Count(a => a.Type == ActivityType.Call), totalFollowUps = activities.Count(a => a.Type == ActivityType.FollowUp),
                totalProposals = activities.Count(a => a.Type == ActivityType.Proposal),
                inSchoolMinutes = (int)inSchoolMin, avgFraudScore,
                routeCompliancePercent = routeCompliance, missedAssignments
            },
            dayBreakdown,
            leads = new
            {
                newLeads = foLeads.Count(l => l.CreatedAt >= startUtc && l.CreatedAt < endUtc),
                totalActive = foLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost),
                totalLost = foLeads.Count(l => l.Stage == LeadStage.Lost),
                lostLeads = foLeads.Where(l => l.Stage == LeadStage.Lost).Select(l => new { school = l.School, value = l.Value, reason = l.LossReason ?? "Not specified" }),
                hotLeads = foLeads.Where(l => l.Score >= 70 && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
                    .Take(5).Select(l => new { school = l.School, stage = l.Stage.ToString(), score = l.Score, value = l.Value }),
                byStage = foLeads.GroupBy(l => l.Stage.ToString()).Select(g => new { stage = g.Key, count = g.Count(), value = g.Sum(l => l.Value) })
            },
            deals = new { closed = deals.Count, totalValue = deals.Sum(d => d.FinalValue) },
            targets = target != null ? new
            {
                title = target.Title ?? "", periodType = target.PeriodType.ToString(),
                targetAmount = target.TargetAmount, achievedAmount = target.AchievedAmount,
                percentComplete = target.TargetAmount > 0 ? Math.Round((double)target.AchievedAmount / (double)target.TargetAmount * 100, 1) : 0,
                targetSchools = target.NumberOfSchools, achievedSchools = target.AchievedSchools,
                daysRemaining = (target.EndDate - DateTime.UtcNow).Days
            } : (object?)null,
            schoolCoverage = new { visited = visitedSchoolIds, total = totalSchools, coveragePct = totalSchools > 0 ? Math.Round((double)visitedSchoolIds / totalSchools * 100, 1) : 0 },
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
    }

    public async Task<AiReport> GenerateManagementReportAsync(int managerId, AiReportType reportType, DateTime periodStart, DateTime periodEnd)
    {
        var startUtc = DateTime.SpecifyKind(periodStart.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(periodEnd.Date, DateTimeKind.Utc);

        var existing = await _uow.AiReports.Query()
            .FirstOrDefaultAsync(r => r.UserId == managerId && r.ReportType == reportType && r.PeriodStart == startUtc && r.PeriodEnd == endUtc
                                       && (r.Status == AiReportStatus.Completed || r.Status == AiReportStatus.Generating));
        if (existing != null) return existing;

        var report = new AiReport
        {
            UserId = managerId, ReportType = reportType,
            ReportDate = endUtc, PeriodStart = startUtc, PeriodEnd = endUtc,
            Status = AiReportStatus.Pending
        };
        await _uow.AiReports.AddAsync(report);
        await _uow.SaveChangesAsync();

        try
        {
            var inputJson = await CollectManagementDataAsync(managerId, periodStart, periodEnd);
            report.InputDataJson = inputJson;
            report.Status = AiReportStatus.Generating;
            await _uow.AiReports.UpdateAsync(report);
            await _uow.SaveChangesAsync();

            var systemPrompt = GetMgmtSystemPrompt();
            var geminiResult = await _gemini.GenerateContentAsync(systemPrompt, inputJson);

            if (!geminiResult.Success)
            {
                report.Status = AiReportStatus.Failed;
                report.ErrorMessage = geminiResult.Error;
                await _uow.AiReports.UpdateAsync(report);
                await _uow.SaveChangesAsync();
                return report;
            }

            report.OutputJson = geminiResult.Content;
            report.GeminiTokensUsed = geminiResult.TokensUsed;
            report.GeneratedAt = DateTime.UtcNow;

            try
            {
                using var doc = JsonDocument.Parse(geminiResult.Content);
                // Try multiple key names Gemini might use
                foreach (var key in new[] { "overallHealthScore", "overallScore", "healthScore", "score" })
                    if (doc.RootElement.TryGetProperty(key, out var sp) && sp.ValueKind == JsonValueKind.Number)
                    { report.OverallScore = sp.GetInt32(); break; }
                foreach (var key in new[] { "overallRating", "rating", "healthRating" })
                    if (doc.RootElement.TryGetProperty(key, out var rp) && rp.ValueKind == JsonValueKind.String)
                    { report.OverallRating = rp.GetString() ?? ""; break; }
                // Derive rating from score if still empty
                if (string.IsNullOrEmpty(report.OverallRating))
                    report.OverallRating = report.OverallScore >= 70 ? "Good" : report.OverallScore >= 50 ? "Average" : "Poor";
            }
            catch { }

            report.Status = AiReportStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate management report for user {ManagerId}", managerId);
            report.Status = AiReportStatus.Failed;
            report.ErrorMessage = ex.Message;
        }

        await _uow.AiReports.UpdateAsync(report);
        await _uow.SaveChangesAsync();
        return report;
    }

    // ─── Batch Operations (sequential — EF Core DbContext is not thread-safe) ──
    public async Task GenerateAllFoDailyReportsAsync(DateTime date)
    {
        var fos = await _uow.Users.Query().Where(u => u.Role == UserRole.FO).ToListAsync();
        foreach (var fo in fos)
        {
            try
            {
                await GenerateFoDailyReportAsync(fo.Id, date);
                _logger.LogInformation("Generated FO daily report for {FoName} ({FoId})", fo.Name, fo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate report for FO {FoId}", fo.Id);
            }
        }
    }

    public async Task GenerateAllFoWeeklyReportsAsync(DateTime weekStart, DateTime weekEnd)
    {
        var fos = await _uow.Users.Query().Where(u => u.Role == UserRole.FO).ToListAsync();
        foreach (var fo in fos)
        {
            try
            {
                await GenerateFoPeriodReportAsync(fo.Id, AiReportType.FoWeekly, weekStart, weekEnd);
                _logger.LogInformation("Generated FO weekly report for {FoName}", fo.Name);
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed FO weekly for {FoId}", fo.Id); }
        }
    }

    public async Task GenerateAllFoMonthlyReportsAsync(DateTime monthStart, DateTime monthEnd)
    {
        var fos = await _uow.Users.Query().Where(u => u.Role == UserRole.FO).ToListAsync();
        foreach (var fo in fos)
        {
            try
            {
                await GenerateFoPeriodReportAsync(fo.Id, AiReportType.FoMonthly, monthStart, monthEnd);
                _logger.LogInformation("Generated FO monthly report for {FoName}", fo.Name);
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed FO monthly for {FoId}", fo.Id); }
        }
    }

    public async Task GenerateAllManagementReportsAsync(DateTime periodStart, DateTime periodEnd)
    {
        var managers = await _uow.Users.Query()
            .Where(u => u.Role == UserRole.ZH || u.Role == UserRole.RH || u.Role == UserRole.SH || u.Role == UserRole.SCA)
            .ToListAsync();

        foreach (var mgr in managers)
        {
            try
            {
                var reportType = mgr.Role switch
                {
                    UserRole.ZH => AiReportType.ZhWeekly,
                    UserRole.RH => AiReportType.RhWeekly,
                    UserRole.SH => AiReportType.ShWeekly,
                    UserRole.SCA => AiReportType.ScaWeekly,
                    _ => AiReportType.ZhWeekly
                };
                await GenerateManagementReportAsync(mgr.Id, reportType, periodStart, periodEnd);
                _logger.LogInformation("Generated management report for {Name} ({Role})", mgr.Name, mgr.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate management report for {ManagerId}", mgr.Id);
            }
        }
    }

    // ─── Retrieval ──────────────────────────────────────────────────
    public async Task<AiReportDetailDto?> GetReportAsync(int reportId, int requestingUserId, string requestingUserRole)
    {
        var report = await _uow.AiReports.Query()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reportId);
        if (report == null) return null;

        // Access check
        if (!await CanAccessReport(report, requestingUserId, requestingUserRole))
            return null;

        return new AiReportDetailDto
        {
            Id = report.Id, UserId = report.UserId, UserName = report.User?.Name ?? "",
            UserRole = report.User?.Role.ToString() ?? "",
            ReportType = report.ReportType.ToString(), ReportDate = report.ReportDate,
            PeriodStart = report.PeriodStart, PeriodEnd = report.PeriodEnd,
            OutputJson = report.OutputJson, OverallScore = report.OverallScore,
            OverallRating = report.OverallRating, Status = report.Status.ToString(),
            GeneratedAt = report.GeneratedAt
        };
    }

    public async Task<List<AiReportListDto>> GetReportsAsync(int requestingUserId, string requestingUserRole, AiReportFilterDto filters)
    {
        var query = _uow.AiReports.Query().Include(r => r.User).AsQueryable();

        // Role-based scoping
        if (Enum.TryParse<UserRole>(requestingUserRole, out var role))
        {
            if (role == UserRole.FO)
            {
                query = query.Where(r => r.UserId == requestingUserId);
            }
            else if (role == UserRole.ZH)
            {
                var user = await _uow.Users.GetByIdAsync(requestingUserId);
                if (user?.ZoneId != null)
                {
                    var zoneUserIds = await _uow.Users.Query()
                        .Where(u => u.ZoneId == user.ZoneId).Select(u => u.Id).ToListAsync();
                    query = query.Where(r => zoneUserIds.Contains(r.UserId) || r.UserId == requestingUserId);
                }
            }
            else if (role == UserRole.RH)
            {
                var user = await _uow.Users.GetByIdAsync(requestingUserId);
                if (user?.RegionId != null)
                {
                    var regionUserIds = await _uow.Users.Query()
                        .Where(u => u.RegionId == user.RegionId).Select(u => u.Id).ToListAsync();
                    query = query.Where(r => regionUserIds.Contains(r.UserId) || r.UserId == requestingUserId);
                }
            }
            // SH and SCA see all
        }

        // Filters
        if (!string.IsNullOrEmpty(filters.ReportType) && Enum.TryParse<AiReportType>(filters.ReportType, out var rtFilter))
            query = query.Where(r => r.ReportType == rtFilter);
        if (filters.UserId.HasValue)
            query = query.Where(r => r.UserId == filters.UserId.Value);
        if (DateTime.TryParse(filters.DateFrom, out var from))
            query = query.Where(r => r.ReportDate >= DateTime.SpecifyKind(from.Date, DateTimeKind.Utc));
        if (DateTime.TryParse(filters.DateTo, out var to))
            query = query.Where(r => r.ReportDate <= DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReportDate)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync();

        return items.Select(r => new AiReportListDto
        {
            Id = r.Id, UserId = r.UserId, UserName = r.User?.Name ?? "",
            UserRole = r.User?.Role.ToString() ?? "",
            ReportType = r.ReportType.ToString(), ReportDate = r.ReportDate,
            PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd,
            OverallScore = r.OverallScore, OverallRating = r.OverallRating,
            Status = r.Status.ToString(), GeneratedAt = r.GeneratedAt
        }).ToList();
    }

    // ─── Helpers ────────────────────────────────────────────────────
    private async Task<bool> CanAccessReport(AiReport report, int requestingUserId, string requestingUserRole)
    {
        if (!Enum.TryParse<UserRole>(requestingUserRole, out var role)) return false;
        if (role == UserRole.SH || role == UserRole.SCA) return true;
        if (report.UserId == requestingUserId) return true;

        var requestingUser = await _uow.Users.GetByIdAsync(requestingUserId);
        var reportUser = report.User ?? await _uow.Users.GetByIdAsync(report.UserId);
        if (requestingUser == null || reportUser == null) return false;

        if (role == UserRole.ZH) return reportUser.ZoneId == requestingUser.ZoneId;
        if (role == UserRole.RH) return reportUser.RegionId == requestingUser.RegionId;
        return false;
    }

    private static double HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var R = 6371.0;
        var dLat = ToRad((double)(lat2 - lat1));
        var dLon = ToRad((double)(lon2 - lon1));
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    private static string[] TryParseStops(string? stopsJson)
    {
        if (string.IsNullOrEmpty(stopsJson)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(stopsJson) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}
