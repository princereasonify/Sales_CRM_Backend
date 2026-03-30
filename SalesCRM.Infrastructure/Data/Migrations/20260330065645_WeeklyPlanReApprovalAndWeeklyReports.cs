using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyPlanReApprovalAndWeeklyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedPlanData",
                table: "WeeklyPlans",
                type: "text",
                nullable: true);

            // Rename BiWeekly report types to Weekly
            migrationBuilder.Sql("UPDATE \"AiReports\" SET \"ReportType\" = 'ZhWeekly' WHERE \"ReportType\" = 'ZhBiWeekly'");
            migrationBuilder.Sql("UPDATE \"AiReports\" SET \"ReportType\" = 'RhWeekly' WHERE \"ReportType\" = 'RhBiWeekly'");
            migrationBuilder.Sql("UPDATE \"AiReports\" SET \"ReportType\" = 'ShWeekly' WHERE \"ReportType\" = 'ShBiWeekly'");
            migrationBuilder.Sql("UPDATE \"AiReports\" SET \"ReportType\" = 'ScaWeekly' WHERE \"ReportType\" = 'ScaBiWeekly'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedPlanData",
                table: "WeeklyPlans");
        }
    }
}
