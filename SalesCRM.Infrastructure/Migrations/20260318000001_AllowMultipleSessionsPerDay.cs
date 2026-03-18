using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleSessionsPerDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique constraint so multiple sessions per day are allowed
            migrationBuilder.DropIndex(
                name: "IX_TrackingSessions_UserId_SessionDate",
                table: "TrackingSessions");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_UserId_SessionDate",
                table: "TrackingSessions",
                columns: new[] { "UserId", "SessionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackingSessions_UserId_SessionDate",
                table: "TrackingSessions");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_UserId_SessionDate",
                table: "TrackingSessions",
                columns: new[] { "UserId", "SessionDate" },
                unique: true);
        }
    }
}
