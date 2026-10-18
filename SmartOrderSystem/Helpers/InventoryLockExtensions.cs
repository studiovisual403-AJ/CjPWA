using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;

namespace SmartOrderSystem.Helpers
{
    // Pessimistic row-level locking (SELECT ... FOR UPDATE).
    // DAPAT tawagin sa loob ng database transaction. Nare-release ang lock sa Commit/Rollback.
    public static class InventoryLockExtensions
    {
        // Pending reservations hold capacity until they are approved or expire. The
        // inventory row must already be locked by the caller so reservation creation,
        // checkout, and approval all observe capacity changes in a consistent order.
        public static async Task<long> GetPendingReservationQuantityAsync(
            this ApplicationDbContext context, int inventoryId, DateTime now)
        {
            return await context.Reservations
                .Where(r => r.inventory_id == inventoryId && r.status == "Pending" &&
                            r.expires_at.HasValue && r.expires_at > now)
                .SumAsync(r => (long?)r.quantity) ?? 0L;
        }

        // Naka-sort ang IDs para laging pare-pareho ang pagkakasunod ng lock (iwas deadlock)
        public static async Task<Dictionary<int, ShoeInventory>> LockInventoryRowsAsync(
            this ApplicationDbContext context, IEnumerable<int> inventoryIds)
        {
            var locked = new Dictionary<int, ShoeInventory>();

            foreach (var id in inventoryIds.Distinct().OrderBy(x => x))
            {
                var wasTracked = context.ChangeTracker.Entries<ShoeInventory>()
                    .Any(e => e.Entity.inventory_id == id);

                var row = (await context.ShoeInventories
                    .FromSqlInterpolated($"SELECT * FROM shoe_inventory WHERE inventory_id = {id} FOR UPDATE")
                    .ToListAsync()).FirstOrDefault();

                if (row == null) continue;

                // Kung na-load na ito dati, luma ang values sa memory: i-reload pagkatapos ng lock
                if (wasTracked) await context.Entry(row).ReloadAsync();

                locked[id] = row;
            }

            return locked;
        }

        public static async Task<ShoeInventory?> LockInventoryBySizeAsync(
            this ApplicationDbContext context, int shoeId, decimal size)
        {
            var row = (await context.ShoeInventories
                .FromSqlInterpolated($"SELECT * FROM shoe_inventory WHERE shoe_id = {shoeId} AND size = {size} FOR UPDATE")
                .ToListAsync()).FirstOrDefault();

            if (row != null && context.Entry(row).State != EntityState.Added)
                await context.Entry(row).ReloadAsync();

            return row;
        }

        public static async Task<Reservation?> LockReservationAsync(
            this ApplicationDbContext context, int reservationId)
        {
            var wasTracked = context.ChangeTracker.Entries<Reservation>()
                .Any(e => e.Entity.reservation_id == reservationId);

            var row = (await context.Reservations
                .FromSqlInterpolated($"SELECT * FROM reservations WHERE reservation_id = {reservationId} FOR UPDATE")
                .ToListAsync()).FirstOrDefault();

            if (row != null && wasTracked) await context.Entry(row).ReloadAsync();

            return row;
        }
    }
}
