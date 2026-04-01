using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGeofenceRadiusTo50m : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Schools\" SET \"GeofenceRadiusMetres\" = 50 WHERE \"GeofenceRadiusMetres\" = 100;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Schools\" SET \"GeofenceRadiusMetres\" = 100 WHERE \"GeofenceRadiusMetres\" = 50;");
        }
    }
}
