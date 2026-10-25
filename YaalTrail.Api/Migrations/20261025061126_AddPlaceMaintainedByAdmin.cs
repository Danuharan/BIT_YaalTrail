using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaalTrail.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceMaintainedByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaintainedByAdminId",
                table: "Places",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_MaintainedByAdminId",
                table: "Places",
                column: "MaintainedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Places_AdminUsers_MaintainedByAdminId",
                table: "Places",
                column: "MaintainedByAdminId",
                principalTable: "AdminUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Places_AdminUsers_MaintainedByAdminId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_MaintainedByAdminId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "MaintainedByAdminId",
                table: "Places");
        }
    }
}
