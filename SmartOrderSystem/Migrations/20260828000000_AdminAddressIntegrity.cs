using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartOrderSystem.Migrations
{
    public partial class AdminAddressIntegrity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_address_id",
                table: "orders",
                column: "address_id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customer_addresses_address_id",
                table: "orders",
                column: "address_id",
                principalTable: "customer_addresses",
                principalColumn: "address_id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_customer_addresses_address_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_address_id",
                table: "orders");
        }
    }
}
