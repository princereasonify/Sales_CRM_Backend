using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TravelAllowanceRate",
                table: "Users",
                type: "numeric(6,2)",
                nullable: false,
                defaultValue: 10.00m);

            migrationBuilder.CreateTable(
                name: "TrackingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalDistanceKm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    AllowanceAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    AllowanceRatePerKm = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AllowanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalDistanceKm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    RatePerKm = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    GrossAllowance = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyAllowances_TrackingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrackingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyAllowances_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DailyAllowances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationPings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", nullable: false),
                    AccuracyMetres = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    SpeedKmh = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    AltitudeMetres = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    DistanceFromPrevKm = table.Column<decimal>(type: "numeric(10,5)", nullable: false),
                    CumulativeDistanceKm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    InvalidReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationPings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationPings_TrackingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrackingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationPings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyAllowances_AllowanceDate",
                table: "DailyAllowances",
                column: "AllowanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAllowances_ApprovedById",
                table: "DailyAllowances",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAllowances_SessionId",
                table: "DailyAllowances",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyAllowances_UserId",
                table: "DailyAllowances",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_RecordedAt",
                table: "LocationPings",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_SessionId",
                table: "LocationPings",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_UserId",
                table: "LocationPings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_SessionDate",
                table: "TrackingSessions",
                column: "SessionDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_Status",
                table: "TrackingSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_UserId_SessionDate",
                table: "TrackingSessions",
                columns: new[] { "UserId", "SessionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyAllowances");

            migrationBuilder.DropTable(
                name: "LocationPings");

            migrationBuilder.DropTable(
                name: "TrackingSessions");

            migrationBuilder.DropColumn(
                name: "TravelAllowanceRate",
                table: "Users");
        }
    }
}
