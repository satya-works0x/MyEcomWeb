using MyEcomWeb.Data;
using MyEcomWeb.Models.Entities;
using MyEcomWeb.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MyEcomWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return  role == "Admin" || role == "SuperAdmin";
        }

        public async Task<IActionResult> Index()
        {
            if(!IsAdmin())
                return RedirectToAction("Login", "Account");

            var stats = new
            {
                TotalOrders = await _context.Orders.CountAsync(),
                TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalUsers = await _context.Users.CountAsync(u => u.IsActive),
                TotalRevenue = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SumAsync(o => o.TotalAmount),
                
                OrdersByStatus = await _context.Orders
                    .GroupBy(o => o.Status)
                    .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count()),

                TopProducts = await _context.OrderItems
                    .GroupBy(oi => oi.ProductName )
                    .Select(g => new{Name = g.Key, Sold = g.Sum(oi => oi.Quantity)})
                    .OrderByDescending(x => x.Sold)
                    .Take(5)
                    .ToListAsync()

            };
            return View(stats);
        }

        public async Task<IActionResult> Orders(string? status = null)
        {
            if(!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            IQueryable<order> query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems);

            if(!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Statuses = Enum.GetNames(typeof(OrderStatus));
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus newStatus)
        {
            if(!IsAdmin())
                return Json(new{ success = false, message = "Unauthorized"});
            
            var order = await _context.Orders.FindAsync(orderId);
            if(order == null)
                return Json(new{ success = false, message = "order not found"});

            try
            {
                switch(newStatus)
                {
                    case OrderStatus.Processing:
                        order.MarkAsProcessing();
                        break;
                    case OrderStatus.Shipped:
                        order.MarkAsShipped(order.TrackingNumber ?? Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper());
                        break;
                    case OrderStatus.Delivered:
                        order.MarkAsDelivered();
                        break;
                    case OrderStatus.Cancelled:
                        order.Cancel("Cancelled by admin");
                        break;
                    default:
                        order.Status = newStatus;
                        break;
                }
                await _context.SaveChangesAsync();
                return Json(new {success = true, message = $"order #{orderId} updated to {newStatus}"});
            }
            catch(Exception ex)
            {
                return Json(new{success = false, message = ex.Message});
            }
        }

        public async Task<IActionResult> Products()
        {
            if(!IsAdmin())
                return RedirectToAction("Login", "Account");
            
            var products = await _context.Products
                .Include( p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Users()
        {
            if(!IsAdmin())
                return RedirectToAction("Login", "Account");

            var users = await _context.Users
                .Include(u => u.Orders)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }
    }
}