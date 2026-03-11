using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedById",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AssignedById",
                table: "Leads",
                column: "AssignedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_AssignedById",
                table: "Leads",
                column: "AssignedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_AssignedById",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_AssignedById",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AssignedById",
                table: "Leads");
        }
    }
}
