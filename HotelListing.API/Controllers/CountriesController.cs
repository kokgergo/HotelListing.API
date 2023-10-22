using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelListing.API.Data;
using HotelListing.API.Core.Models.Country;
using AutoMapper;
using HotelListing.API.Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using HotelListing.API.Core.Exceptions;
using HotelListing.API.Core.Models;
using Microsoft.AspNetCore.OData.Query;

namespace HotelListing.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class CountriesController : ControllerBase
    {
        private readonly ICountriesRepository _countriesRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<CountriesController> _logger;

        public CountriesController(ICountriesRepository countriesRepository, IMapper mapper, ILogger<CountriesController> logger)
        {
            this._countriesRepository = countriesRepository;
            _mapper = mapper;
            this._logger = logger;
        }

        // GET: api/Countries/GetAll
        [HttpGet("GetAll")]
        [EnableQuery]
        public async Task<ActionResult<IEnumerable<GetCountryDto>>> GetCountries()
        {
            var countries = await _countriesRepository.GetAllAsync();
            var elements = _mapper.Map<List<GetCountryDto>>(countries);
            return Ok(elements);
        }

        // GET: api/Countries/?StartIndex=0&pagesize=25&pagenumber=1
        [HttpGet]
        public async Task<ActionResult<PagedResult<GetCountryDto>>> GetPagedCountries([FromQuery]QueryParamters queryParamters)
        {
            var pagedCountriesResult = await _countriesRepository.GetAllAsync<GetCountryDto>(queryParamters);
            return Ok(pagedCountriesResult);
        }

        // GET: api/Countries/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CountryDto>> GetCountry(int id)
        {
            var country = await _countriesRepository.GetDetails(id);

            if (country == null)
            {
                throw new NotFoundException(nameof(GetCountry), id);
                _logger.LogWarning($"No record found in {nameof(GetCountry)} with Id: {id}.");
                return NotFound();
            }
            var countryDto = _mapper.Map<CountryDto>(country);
            return countryDto;
        }

        // PUT: api/Countries/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutCountry(int id, UpdateCountryDto updateCountryDto)
        {
            if (id != updateCountryDto.Id)
            {
                return BadRequest();
            }

            //_context.Entry(updateCountryDto).State = EntityState.Modified;
            var country = await _countriesRepository.GetAsync(id);
            if (country == null)
            {
                return NotFound();
            }

            _mapper.Map(updateCountryDto, country);


            try
            {
                await _countriesRepository.UpdateAsync(country);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await CountryExists(id))
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

        // POST: api/Countries
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Country>> PostCountry(CreateCountryDto createCountryDto)
        {
            
            var country = _mapper.Map<Country>(createCountryDto);

            
            await _countriesRepository.AddAsync(country);
           

            return CreatedAtAction("GetCountry", new { id = country.Id }, country);
        }

        // DELETE: api/Countries/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            var country = await _countriesRepository.GetAsync(id);
            if (country == null)
            {
                return NotFound();
            }

            
            await _countriesRepository.DeleteAsync(id);
            return NoContent();
        }

        private async Task<bool> CountryExists(int id)
        {
           
            return await _countriesRepository.Exists(id);
        }
    }
}
