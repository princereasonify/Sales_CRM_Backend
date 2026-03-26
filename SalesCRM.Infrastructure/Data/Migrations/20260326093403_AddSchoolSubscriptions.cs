using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealId = table.Column<int>(type: "integer", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: false),
                    PlanType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SchoolLoginEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchoolLoginPassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CredentialStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CredentialProvisionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CredentialProvisionedById = table.Column<int>(type: "integer", nullable: true),
                    NumberOfLicenses = table.Column<int>(type: "integer", nullable: false),
                    Modules = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolSubscriptions_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolSubscriptions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolSubscriptions_Users_CredentialProvisionedById",
                        column: x => x.CredentialProvisionedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubscriptions_CredentialProvisionedById",
                table: "SchoolSubscriptions",
                column: "CredentialProvisionedById");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubscriptions_DealId",
                table: "SchoolSubscriptions",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubscriptions_SchoolId",
                table: "SchoolSubscriptions",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubscriptions_Status",
                table: "SchoolSubscriptions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolSubscriptions");
        }
    }
}
