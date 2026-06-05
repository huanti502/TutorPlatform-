using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "TutorProfiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "TutorProfiles",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "TutorProfiles");
        }
    }
}