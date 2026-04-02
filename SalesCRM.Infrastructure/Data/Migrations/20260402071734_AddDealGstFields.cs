using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealGstFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountWithoutGst",
                table: "Deals",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Deals",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BillingFrequency",
                table: "Deals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "Deals",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingDate",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Deals",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalLogins",
                table: "Deals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMoney",
                table: "Deals",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountWithoutGst",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "BillingFrequency",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "OnboardingDate",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TotalLogins",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TotalMoney",
                table: "Deals");
        }
    }
}
