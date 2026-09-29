using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartOrderSystem.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileLockoutColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- === Reconcile Customer lockout columns ===
-- The DB may have 'reset_answer_lockout_end' (old name) or may lack the columns
-- entirely because the HashSecurityAnswers + AdminAddressIntegrity migrations
-- were never recognised by EF (no .Designer.cs files).

-- 1. Handle reset_answer_locked_until: rename from old name, copy data, or add
SET @old_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'customers' AND COLUMN_NAME = 'reset_answer_lockout_end');
SET @new_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'customers' AND COLUMN_NAME = 'reset_answer_locked_until');

SET @stmt = IF(@old_exists = 1 AND @new_exists = 0,
    'ALTER TABLE `customers` CHANGE `reset_answer_lockout_end` `reset_answer_locked_until` DATETIME NULL',
    IF(@old_exists = 1 AND @new_exists = 1,
        'UPDATE `customers` SET `reset_answer_locked_until` = `reset_answer_lockout_end` WHERE `reset_answer_locked_until` IS NULL',
        IF(@old_exists = 0 AND @new_exists = 0,
            'ALTER TABLE `customers` ADD COLUMN `reset_answer_locked_until` DATETIME NULL',
            'SELECT 1'
        )
    )
);
PREPARE prep FROM @stmt; EXECUTE prep; DEALLOCATE PREPARE prep;

SET @old_still = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'customers' AND COLUMN_NAME = 'reset_answer_lockout_end');
SET @drop_old = IF(@old_still = 1, 'ALTER TABLE `customers` DROP COLUMN `reset_answer_lockout_end`', 'SELECT 1');
PREPARE prep2 FROM @drop_old; EXECUTE prep2; DEALLOCATE PREPARE prep2;

-- 2. Add reset_answer_failed_attempts if missing
SET @fa_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'customers' AND COLUMN_NAME = 'reset_answer_failed_attempts');
SET @add_fa = IF(@fa_exists = 0, 'ALTER TABLE `customers` ADD COLUMN `reset_answer_failed_attempts` INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE prep3 FROM @add_fa; EXECUTE prep3; DEALLOCATE PREPARE prep3;
");

            migrationBuilder.Sql(@"
-- === Create user_security_answers table if missing ===
CREATE TABLE IF NOT EXISTS `user_security_answers` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `customer_id` INT NOT NULL,
    `question_1` LONGTEXT NOT NULL,
    `answer_1_hash` LONGTEXT NOT NULL,
    `question_2` LONGTEXT NOT NULL,
    `answer_2_hash` LONGTEXT NOT NULL,
    CONSTRAINT `PK_user_security_answers` PRIMARY KEY (`Id`),
    KEY `IX_user_security_answers_customer_id` (`customer_id`)
) DEFAULT CHARSET=utf8mb4;

SET @fk_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_security_answers' AND CONSTRAINT_NAME = 'FK_user_security_answers_customers_customer_id');
SET @add_fk = IF(@fk_exists = 0,
    'ALTER TABLE `user_security_answers` ADD CONSTRAINT `FK_user_security_answers_customers_customer_id` FOREIGN KEY (`customer_id`) REFERENCES `customers`(`customer_id`) ON DELETE CASCADE',
    'SELECT 1'
);
PREPARE prep4 FROM @add_fk; EXECUTE prep4; DEALLOCATE PREPARE prep4;
");

            migrationBuilder.Sql(@"
-- === Add shoes_catalog.created_at if missing ===
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shoes_catalog' AND COLUMN_NAME = 'created_at');
SET @stmt = IF(@col_exists = 0,
    'ALTER TABLE `shoes_catalog` ADD COLUMN `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP',
    'SELECT 1'
);
PREPARE prep FROM @stmt; EXECUTE prep; DEALLOCATE PREPARE prep;
");

            migrationBuilder.Sql(@"
-- === Create cart_items table if missing ===
CREATE TABLE IF NOT EXISTS `cart_items` (
    `CartItemId` INT NOT NULL AUTO_INCREMENT,
    `CustomerId` INT NOT NULL,
    `ShoeId` INT NOT NULL,
    `Size` VARCHAR(20) NOT NULL,
    `Color` VARCHAR(50) NOT NULL,
    `Quantity` INT NOT NULL,
    `PriceAtAddTime` DECIMAL(10,2) NOT NULL,
    `CreatedAt` DATETIME NOT NULL,
    CONSTRAINT `PK_cart_items` PRIMARY KEY (`CartItemId`)
) DEFAULT CHARSET=utf8mb4;

SET @fk1 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cart_items' AND CONSTRAINT_NAME = 'FK_cart_items_customers_CustomerId');
SET @stmt1 = IF(@fk1 = 0,
    'ALTER TABLE `cart_items` ADD CONSTRAINT `FK_cart_items_customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `customers`(`customer_id`) ON DELETE CASCADE',
    'SELECT 1'
);
PREPARE prep1 FROM @stmt1; EXECUTE prep1; DEALLOCATE PREPARE prep1;

SET @fk2 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cart_items' AND CONSTRAINT_NAME = 'FK_cart_items_shoes_catalog_ShoeId');
SET @stmt2 = IF(@fk2 = 0,
    'ALTER TABLE `cart_items` ADD CONSTRAINT `FK_cart_items_shoes_catalog_ShoeId` FOREIGN KEY (`ShoeId`) REFERENCES `shoes_catalog`(`shoe_id`) ON DELETE CASCADE',
    'SELECT 1'
);
PREPARE prep2 FROM @stmt2; EXECUTE prep2; DEALLOCATE PREPARE prep2;

SET @idx1 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cart_items' AND INDEX_NAME = 'IX_cart_items_CustomerId');
SET @stmt3 = IF(@idx1 = 0, 'ALTER TABLE `cart_items` ADD INDEX `IX_cart_items_CustomerId` (`CustomerId`)', 'SELECT 1');
PREPARE prep3 FROM @stmt3; EXECUTE prep3; DEALLOCATE PREPARE prep3;

SET @idx2 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cart_items' AND INDEX_NAME = 'IX_cart_items_ShoeId');
SET @stmt4 = IF(@idx2 = 0, 'ALTER TABLE `cart_items` ADD INDEX `IX_cart_items_ShoeId` (`ShoeId`)', 'SELECT 1');
PREPARE prep4 FROM @stmt4; EXECUTE prep4; DEALLOCATE PREPARE prep4;
");

            migrationBuilder.Sql(@"
-- === Create product_ratings table if missing ===
CREATE TABLE IF NOT EXISTS `product_ratings` (
    `rating_id` INT NOT NULL AUTO_INCREMENT,
    `shoe_id` INT NOT NULL,
    `customer_id` INT NOT NULL,
    `rating` INT NOT NULL,
    CONSTRAINT `PK_product_ratings` PRIMARY KEY (`rating_id`)
) DEFAULT CHARSET=utf8mb4;
");

            migrationBuilder.Sql(@"
-- === Add FK and index on orders.address_id if missing ===
SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'orders' AND INDEX_NAME = 'IX_orders_address_id');
SET @stmt = IF(@idx_exists = 0, 'ALTER TABLE `orders` ADD INDEX `IX_orders_address_id` (`address_id`)', 'SELECT 1');
PREPARE prep FROM @stmt; EXECUTE prep; DEALLOCATE PREPARE prep;

SET @fk_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'orders' AND CONSTRAINT_NAME = 'FK_orders_customer_addresses_address_id');
SET @stmt = IF(@fk_exists = 0,
    'ALTER TABLE `orders` ADD CONSTRAINT `FK_orders_customer_addresses_address_id` FOREIGN KEY (`address_id`) REFERENCES `customer_addresses`(`address_id`) ON DELETE RESTRICT',
    'SELECT 1'
);
PREPARE prep2 FROM @stmt; EXECUTE prep2; DEALLOCATE PREPARE prep2;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `orders` DROP FOREIGN KEY IF EXISTS `FK_orders_customer_addresses_address_id`;");
            migrationBuilder.Sql("ALTER TABLE `orders` DROP INDEX IF EXISTS `IX_orders_address_id`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `cart_items`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `product_ratings`;");
            migrationBuilder.Sql("ALTER TABLE `shoes_catalog` DROP COLUMN IF EXISTS `created_at`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `user_security_answers`;");
            migrationBuilder.Sql("ALTER TABLE `customers` DROP COLUMN IF EXISTS `reset_answer_failed_attempts`;");
            migrationBuilder.Sql("ALTER TABLE `customers` DROP COLUMN IF EXISTS `reset_answer_locked_until`;");
        }
    }
}
