using ApiRedis.Data;
using ApiRedis.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ApiRedis.Controllers
{
    public class ProductController : ControllerBase//não defina tempo de validade (os dados estão em um servidor)
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;

        public ProductController(AppDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetProduct(int id)
        {
            var cachedProduct = await _cache.GetStringAsync($"product_{id}");
            if (cachedProduct != null)
            {
                return Ok(JsonSerializer.Deserialize<Product>(cachedProduct));
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            };
            // Cache do produto
            await _cache.SetStringAsync($"product_{id}", JsonSerializer.Serialize(product), cacheOptions);

            return Ok(product);
        }

        [HttpPost("enserir")]
        public async Task<ActionResult> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            };

            // Cache do novo produto
            await _cache.SetStringAsync($"product_{product.Id}", JsonSerializer.Serialize(product), cacheOptions);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id,[FromBody] Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            };

            // Atualizar o cache
            await _cache.SetStringAsync($"product_{product.Id}", JsonSerializer.Serialize(product), cacheOptions);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            // Remover do cache
            await _cache.RemoveAsync($"product_{id}");

            return NoContent();
        }
    }
}
