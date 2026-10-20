using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrderApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Email", "Name" },
                values: new object[,]
                {
                    { 1, "alice@example.com", "Alice Johnson" },
                    { 2, "bob@example.com", "Bob Smith" },
                    { 3, "carol@example.com", "Carol White" },
                    { 4, "david@example.com", "David Brown" },
                    { 5, "eve@example.com", "Eve Davis" },
                    { 6, "frank@example.com", "Frank Miller" },
                    { 7, "grace@example.com", "Grace Wilson" },
                    { 8, "henry@example.com", "Henry Moore" },
                    { 9, "isla@example.com", "Isla Taylor" },
                    { 10, "jack@example.com", "Jack Anderson" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Name", "Price", "Sku", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "Laptop Pro 15", 1299.99m, "LAP-001", 50 },
                    { 2, "Wireless Mouse", 29.99m, "MOU-001", 200 },
                    { 3, "Mechanical Keyboard", 89.99m, "KEY-001", 75 },
                    { 4, "27-inch Monitor", 349.99m, "MON-001", 30 },
                    { 5, "USB-C Hub", 49.99m, "HUB-001", 150 },
                    { 6, "Webcam HD 1080p", 69.99m, "CAM-001", 60 },
                    { 7, "Noise-Cancelling Headphones", 199.99m, "HDP-001", 40 },
                    { 8, "External SSD 1TB", 119.99m, "SSD-001", 80 },
                    { 9, "Desk Lamp LED", 34.99m, "LMP-001", 100 },
                    { 10, "Ergonomic Chair", 499.99m, "CHR-001", 15 },
                    { 11, "Standing Desk", 699.99m, "DSK-001", 10 },
                    { 12, "HDMI Cable 2m", 12.99m, "CBL-001", 300 },
                    { 13, "Cable Management Kit", 19.99m, "CMK-001", 120 },
                    { 14, "Laptop Stand", 39.99m, "STD-001", 90 },
                    { 15, "Mousepad XL", 24.99m, "PAD-001", 180 },
                    { 16, "Smart Power Strip", 44.99m, "PWR-001", 55 },
                    { 17, "Portable Charger 20000mAh", 54.99m, "CHG-001", 70 },
                    { 18, "Bluetooth Speaker", 79.99m, "SPK-001", 1 },
                    { 19, "Desk Organiser", 27.99m, "ORG-001", 0 },
                    { 20, "Mini Projector", 249.99m, "PRJ-001", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_OrderDate",
                table: "Orders",
                columns: new[] { "CustomerId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
