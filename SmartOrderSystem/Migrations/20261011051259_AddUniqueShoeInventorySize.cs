using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueShoeInventorySize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_shoe_inventory_shoe_id_size",
                table: "shoe_inventory",
                columns: new[] { "shoe_id", "size" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_shoe_inventory_shoe_id",
                table: "shoe_inventory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_shoe_inventory_shoe_id",
                table: "shoe_inventory",
                column: "shoe_id");

            migrationBuilder.DropIndex(
                name: "IX_shoe_inventory_shoe_id_size",
                table: "shoe_inventory");
        }
    }
}
