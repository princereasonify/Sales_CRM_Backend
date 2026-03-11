using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AchievedLogins",
                table: "TargetAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AchievedStudents",
                table: "TargetAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfLogins",
                table: "TargetAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfStudents",
                table: "TargetAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractEndDate",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractPdfUrl",
                table: "Deals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractStartDate",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfLicenses",
                table: "Deals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Deals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attendees",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConductedBy",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemoMode",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestLevel",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextAction",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpDate",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonDesignation",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonMet",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonPhone",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeIn",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeOut",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AchievedLogins",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "AchievedStudents",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "NumberOfLogins",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "NumberOfStudents",
                table: "TargetAssignments");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ContractPdfUrl",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ContractStartDate",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "NumberOfLicenses",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Attendees",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ConductedBy",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "DemoMode",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "InterestLevel",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "NextAction",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "NextFollowUpDate",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PersonDesignation",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PersonMet",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PersonPhone",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TimeIn",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TimeOut",
                table: "Activities");
        }
    }
}
