using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Models;

namespace SmartOrderSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<ShoeInventory> ShoeInventories { get; set; }
        public DbSet<ShoeCatalog> ShoeCatalogs { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductRating> ProductRatings { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProductPromotion> ProductPromotions { get; set; }
        public DbSet<UserSecurityAnswer> UserSecurityAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.ShippingAddress)
                .WithMany()
                .HasForeignKey(order => order.address_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShoeInventory>()
                .HasOne(inventory => inventory.ShoeCatalog)
                .WithMany()
                .HasForeignKey(inventory => inventory.shoe_id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductImage>()
                .HasOne(image => image.ShoeCatalog)
                .WithMany(catalog => catalog.Images)
                .HasForeignKey(image => image.ShoeCatalogId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}