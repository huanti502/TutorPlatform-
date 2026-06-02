using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorPlatform.Infrastructure.Migrations
{
    public partial class AddMissingSubjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm 5 môn học còn thiếu (Id 6-10) vào DB hiện có
            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Name", "Level", "IsActive" },
                values: new object[,]
                {
                    { 6,  "Ngữ văn",      "THPT",       true },
                    { 7,  "Lịch sử",      "THPT",       true },
                    { 8,  "Tiếng Nhật",   "Đại học",    true },
                    { 9,  "Toán cao cấp", "Đại học",    true },
                    { 10, "IELTS",        "Chứng chỉ",  true }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Subjects", keyColumn: "Id", keyValues: new object[] { 6, 7, 8, 9, 10 });
        }
    }
}