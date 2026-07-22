using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Take_Leave.Migrations
{
    /// <inheritdoc />
    public partial class NoticeDepartmentManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notices_Departments_DepartmentId",
                table: "Notices");

            migrationBuilder.DropIndex(
                name: "IX_Notices_DepartmentId",
                table: "Notices");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Notices");

            migrationBuilder.CreateTable(
                name: "DepartmentNotice",
                columns: table => new
                {
                    DepartmentsId = table.Column<int>(type: "int", nullable: false),
                    NoticeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentNotice", x => new { x.DepartmentsId, x.NoticeId });
                    table.ForeignKey(
                        name: "FK_DepartmentNotice_Departments_DepartmentsId",
                        column: x => x.DepartmentsId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentNotice_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentNotice_NoticeId",
                table: "DepartmentNotice",
                column: "NoticeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentNotice");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Notices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notices_DepartmentId",
                table: "Notices",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notices_Departments_DepartmentId",
                table: "Notices",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }
    }
}
