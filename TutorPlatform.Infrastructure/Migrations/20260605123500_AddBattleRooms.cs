using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutorPlatform.Infrastructure.Migrations
{
    public partial class AddBattleRooms : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    RoomId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),

                    SubjectId = table.Column<int>(type: "integer", nullable: false),

                    SubjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),

                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),

                    Player1Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),

                    Player1Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),

                    Player2Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),

                    Player2Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),

                    Player1ConnectionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),

                    Player2ConnectionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),

                    Score1 = table.Column<int>(type: "integer", nullable: false),

                    Score2 = table.Column<int>(type: "integer", nullable: false),

                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),

                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleRooms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleRooms_RoomId",
                table: "BattleRooms",
                column: "RoomId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleRooms");
        }
    }
}