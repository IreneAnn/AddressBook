using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using System.Text.Json;

namespace AddressBook.Api.Controllers
{
    [Route("api/[controller]")]   
    [ApiController]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IMemoryCache _memoryCache;
        public ContactsController(IContactService contactService, IMemoryCache memoryCache)
        {
            _contactService = contactService;
            _memoryCache = memoryCache; 
        }


        [HttpPost]
        [Authorize(Policy = "WriteScope")]
        public async Task<IActionResult> UpsertContact([FromBody] ContactDto contactDto)
        {
            if (contactDto == null)
                return BadRequest("Contact data is required."); //400

            try
            {
                var result = await _contactService.UpsertContactAsync(contactDto);

                if (result?.Status == UpsertStatus.Created)
                    return CreatedAtAction(nameof(UpsertContact), new { id = result.ContactDto.Id }, result.ContactDto); // 201
                else // Updated
                    return Ok(result?.ContactDto); //200
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred  in {nameof(UpsertContact)}: {ex.Message}");
            }           
            
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetContactById(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest("Invalid contact id."); // 400

            try
            {
                var result = await _contactService.GetContactByIdAsync(id);

                if (result == null)
                    return NotFound("Contact with id not found."); // 404

                return Ok(result); // 200
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,$"An unexpected error occurred in {nameof(GetContactById)}: {ex.Message}"); // 500             
            }           
        }

        [HttpGet]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetContactList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                {
                    return BadRequest("Page and pageSize must be greater than zero.");
                }

                // Generate a cache key unique per user and query parameters
                var cacheKey = $"contacts_{User.Identity?.Name}_{page}_{pageSize}";

                if (!_memoryCache.TryGetValue(cacheKey, out (IEnumerable<ContactDto> Items, int Total) cachedResult))
                {
                    // Cache miss: fetch from service
                    cachedResult = await _contactService.GetContactListAsync(page, pageSize);

                    // Cache options: expire after 60 seconds
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    };

                    _memoryCache.Set(cacheKey, cachedResult, cacheOptions);
                }

                if (cachedResult.Total == 0)
                {
                    return NotFound("No contacts found.");
                }
                // Add pagination headers
                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(new
                {
                    cachedResult.Total,
                    pageSize,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(cachedResult.Total / (double)pageSize)
                }));

                return Ok(cachedResult.Items);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred while fetching contacts in {nameof(GetContactList)}: {ex.Message}.");                
            }

        }

    }
}
