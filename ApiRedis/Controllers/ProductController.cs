using ApiRedis.Data;
using ApiRedis.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ApiRedis.Controllers
{
    public class ProductController : ControllerBase//não defina tempo de validade (os dados estão em um servidor)
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _Rcache;
        private readonly IMemoryCache _Mcache;

        public ProductController(IMemoryCache _Memcache,AppDbContext context, IDistributedCache Rediscache)
        {
            _context = context;
            _Rcache = Rediscache;
            _Mcache = _Memcache;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetProduct(int id)
        {
            // layer 1
            if(_Mcache.TryGetValue($"product_{id}", out Product product))
            {
                Console.WriteLine("Layer 1");
                return Ok(product);
                
            }
            // layer 2
            var cachedProduct = await _Rcache.GetStringAsync($"product_{id}");
            if (cachedProduct != null)
            {
                var productFromRedis = JsonSerializer.Deserialize<Product>(cachedProduct);
                var Cacheoptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(10)
                };
                _Mcache.Set($"product_{id}", productFromRedis, Cacheoptions);
                Console.WriteLine("Layer 2");
                return Ok(productFromRedis);

            }
            // layer 3
            product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            //expiration & saveCache Memorycache
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(10)

            };
            _Mcache.Set($"product_{id}", product, options);

            //expirations & saveCache RedisServer
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            };
            await _Rcache.SetStringAsync($"product_{id}", JsonSerializer.Serialize(product), cacheOptions);
            Console.WriteLine("Layer 3");
            return Ok(product);
        }

        [HttpPost("enserir")]
        public async Task<ActionResult> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Atualiza todas as camadas de cache
            var memoryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            _Mcache.Set($"product_{product.Id}", product, memoryOptions);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _Rcache.SetStringAsync($"product_{product.Id}", JsonSerializer.Serialize(product), cacheOptions);

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
            // Atualiza todas as camadas de cache
            var memoryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            _Mcache.Set($"product_{product.Id}", product, memoryOptions);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _Rcache.SetStringAsync($"product_{product.Id}", JsonSerializer.Serialize(product), cacheOptions);

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
            _Mcache.Remove($"product_{id}");
            await _Rcache.RemoveAsync($"product_{id}");

            return NoContent();
        }
    }
}
