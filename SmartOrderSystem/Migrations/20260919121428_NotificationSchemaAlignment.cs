using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSchemaAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_orders_order_id",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "order_id",
                table: "Notifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "promotion_id",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shoe_id",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "Notifications",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_promotion_id",
                table: "Notifications",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_shoe_id",
                table: "Notifications",
                column: "shoe_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_orders_order_id",
                table: "Notifications",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "order_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_product_promotions_promotion_id",
                table: "Notifications",
                column: "promotion_id",
                principalTable: "product_promotions",
                principalColumn: "promotion_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_shoes_catalog_shoe_id",
                table: "Notifications",
                column: "shoe_id",
                principalTable: "shoes_catalog",
                principalColumn: "shoe_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_orders_order_id",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_product_promotions_promotion_id",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_shoes_catalog_shoe_id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_promotion_id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_shoe_id",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "promotion_id",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "shoe_id",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "title",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "order_id",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_orders_order_id",
                table: "Notifications",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "order_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
