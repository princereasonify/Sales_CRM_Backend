using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HighPrecisionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FilteredDistanceKm",
                table: "TrackingSessions",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FraudFlags",
                table: "TrackingSessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FraudScore",
                table: "TrackingSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspicious",
                table: "TrackingSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RawDistanceKm",
                table: "TrackingSessions",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReconstructedDistanceKm",
                table: "TrackingSessions",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BatteryLevel",
                table: "LocationPings",
                type: "numeric(3,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClusterGroup",
                table: "LocationPings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterReason",
                table: "LocationPings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFiltered",
                table: "LocationPings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMocked",
                table: "LocationPings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "LocationPings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilteredDistanceKm",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "FraudFlags",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "FraudScore",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "IsSuspicious",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "RawDistanceKm",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "ReconstructedDistanceKm",
                table: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "BatteryLevel",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "ClusterGroup",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "FilterReason",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "IsFiltered",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "IsMocked",
                table: "LocationPings");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "LocationPings");
        }
    }
}
