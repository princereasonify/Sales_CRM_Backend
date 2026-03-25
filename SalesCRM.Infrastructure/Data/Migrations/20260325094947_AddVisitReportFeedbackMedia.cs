using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitReportFeedbackMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioNotes",
                table: "VisitReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackPersonDesignation",
                table: "VisitReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackPersonName",
                table: "VisitReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedbackSentiment",
                table: "VisitReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackText",
                table: "VisitReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Videos",
                table: "VisitReports",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioNotes",
                table: "VisitReports");

            migrationBuilder.DropColumn(
                name: "FeedbackPersonDesignation",
                table: "VisitReports");

            migrationBuilder.DropColumn(
                name: "FeedbackPersonName",
                table: "VisitReports");

            migrationBuilder.DropColumn(
                name: "FeedbackSentiment",
                table: "VisitReports");

            migrationBuilder.DropColumn(
                name: "FeedbackText",
                table: "VisitReports");

            migrationBuilder.DropColumn(
                name: "Videos",
                table: "VisitReports");
        }
    }
}
