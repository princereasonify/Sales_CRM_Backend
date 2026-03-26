using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoFeedbackAndAutoLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedbackAudioUrl",
                table: "DemoAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackSentiment",
                table: "DemoAssignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackVideoUrl",
                table: "DemoAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreenRecordingUrl",
                table: "DemoAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackAudioUrl",
                table: "DemoAssignments");

            migrationBuilder.DropColumn(
                name: "FeedbackSentiment",
                table: "DemoAssignments");

            migrationBuilder.DropColumn(
                name: "FeedbackVideoUrl",
                table: "DemoAssignments");

            migrationBuilder.DropColumn(
                name: "ScreenRecordingUrl",
                table: "DemoAssignments");
        }
    }
}
