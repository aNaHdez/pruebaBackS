using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tour_of_heroes_api.Models;
using Dapr.Client;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace tour_of_heroes_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HeroController : ControllerBase
    {
        private readonly HeroContext _context;
        private DaprClient _daprClient;
        const string DAPR_STORE_NAME = "statestore";

        ILogger<HeroController> _logger;

        public HeroController(
            HeroContext context,
            DaprClient client,
            ILogger<HeroController> logger
        )
        {
            _context = context;
            _daprClient = client;
            _logger = logger;
        }

        // GET: api/Hero
        [HttpGet]
        public async Task<IEnumerable<Hero>> GetHeroes()
        {
            // return await _context.Heroes.ToListAsync();

            _logger.LogInformation($"Getting heroes...");

            var heroes = await _daprClient.GetStateAsync<List<Hero>>(DAPR_STORE_NAME, "heroes");

            if (heroes == null)
            {
                _logger.LogInformation($"Not heroes in cache. Updating...");
                heroes = await UpdateCache();
            }

            return heroes;
        }

        private async Task<List<Hero>> UpdateCache()
        {
            var heroes = await _context.Heroes.ToListAsync();

            await _daprClient.SaveStateAsync<List<Hero>>(DAPR_STORE_NAME, "heroes", heroes);

            return heroes;
        }

        // GET: api/Hero/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Hero>> GetHero(int id)
        {
            var hero = await _context.Heroes.FindAsync(id);

            if (hero == null)
            {
                return NotFound();
            }

            return hero;
        }

        // PUT: api/Hero/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHero(int id, Hero hero)
        {
            if (id != hero.Id)
            {
                return BadRequest();
            }

            _context.Entry(hero).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HeroExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Hero
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Hero>> PostHero(Hero hero)
        {
            // _context.Heroes.Add(hero);
            // await _context.SaveChangesAsync();

            using var client = new DaprClientBuilder().Build();

            await client.SaveStateAsync(DAPR_STORE_NAME, "heroes", hero);

            return CreatedAtAction(nameof(GetHero), new { id = hero.Id }, hero);
        }

        // DELETE: api/Hero/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHero(int id)
        {
            var hero = await _context.Heroes.FindAsync(id);
            if (hero == null)
            {
                return NotFound();
            }

            _context.Heroes.Remove(hero);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool HeroExists(int id)
        {
            return _context.Heroes.Any(e => e.Id == id);
        }

        //Service-to-service invocation

        // GET: api/hero/villain/{heroName}
        [HttpGet("villain/{heroName}")]
        public async Task<Villain> GetVillain(string heroName)
        {
            _logger.LogInformation($"Finding the villain for {heroName}...");

            Villain villain = null;

            //https://learn.microsoft.com/es-es/dotnet/architecture/dapr-for-net-developers/service-invocation

            try
            {
                var httpClient = DaprClient.CreateInvokeHttpClient(
                    appId: "tour-of-villains-api",
                    daprEndpoint: "http://localhost:3501"
                );

                var response = await httpClient.GetAsync("/villain");
                var json = await response.Content.ReadAsStringAsync();

                villain = JsonConvert.DeserializeObject<Villain>(json);

                // hero = await _daprClient.InvokeMethodAsync<Hero>(HttpMethod.Get, "tour-of-villains-api", "villain");
            }
            catch (InvocationException ex)
            {
                throw;
            }

            return villain;
        }
    }
}