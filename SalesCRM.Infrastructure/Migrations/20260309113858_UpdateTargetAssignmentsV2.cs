using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTargetAssignmentsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AchievedSchools",
                table: "TargetAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfSchools",
                table: "TargetAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PeriodType",
                table: "TargetAssignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "TargetAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "TargetAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "TargetAssignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AchievedSchools",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "NumberOfSchools",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "PeriodType",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "TargetAssignments");
        }
    }
}
