using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHomeLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HomeLatitude",
                table: "Users",
                type: "numeric(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HomeLongitude",
                table: "Users",
                type: "numeric(10,7)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeLongitude",
                table: "Users");
        }
    }
}
