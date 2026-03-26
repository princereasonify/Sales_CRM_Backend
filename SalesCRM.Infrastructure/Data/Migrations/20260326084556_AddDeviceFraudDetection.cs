using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceFraudDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceFraudAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FraudType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    OtherUserId = table.Column<int>(type: "integer", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFraudAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceFraudAlerts_Users_OtherUserId",
                        column: x => x.OtherUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeviceFraudAlerts_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeviceFraudAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceUniqueId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeviceBrand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceOs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SimCarrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsEmulator = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LoginSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceUniqueId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeviceBrand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceOs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LoginCount = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    TrustLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFraudAlerts_FraudType_DetectedAt",
                table: "DeviceFraudAlerts",
                columns: new[] { "FraudType", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFraudAlerts_OtherUserId",
                table: "DeviceFraudAlerts",
                column: "OtherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFraudAlerts_ReviewedById",
                table: "DeviceFraudAlerts",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFraudAlerts_Status",
                table: "DeviceFraudAlerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFraudAlerts_UserId_DetectedAt",
                table: "DeviceFraudAlerts",
                columns: new[] { "UserId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogins_DeviceFingerprint_LoginAt",
                table: "DeviceLogins",
                columns: new[] { "DeviceFingerprint", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogins_IpAddress",
                table: "DeviceLogins",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogins_UserId_LoginAt",
                table: "DeviceLogins",
                columns: new[] { "UserId", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_DeviceFingerprint",
                table: "UserDevices",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId_DeviceFingerprint",
                table: "UserDevices",
                columns: new[] { "UserId", "DeviceFingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceFraudAlerts");

            migrationBuilder.DropTable(
                name: "DeviceLogins");

            migrationBuilder.DropTable(
                name: "UserDevices");
        }
    }
}
