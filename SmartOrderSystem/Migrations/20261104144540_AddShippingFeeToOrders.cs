using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingFeeToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<decimal>(
             //   name: "shipping_fee",
              //  table: "orders",
               // type: "decimal(65,30)",
               // nullable: false,
               // defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shipping_fee",
                table: "orders");
        }
    }
}
