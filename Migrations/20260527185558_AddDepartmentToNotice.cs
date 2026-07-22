using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Take_Leave.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
