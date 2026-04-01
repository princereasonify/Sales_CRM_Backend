using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHomeAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeAddress",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeAddress",
                table: "Users");
        }
    }
}
