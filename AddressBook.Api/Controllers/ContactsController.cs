using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AddressBook.Api.Controllers
{
    [Route("api/[controller]")]   
    [ApiController]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly ILogger<ContactsController> _logger;
        public ContactsController(IContactService contactService, ILogger<ContactsController> logger)
        {
            _contactService = contactService;            
            _logger = logger;
        }



        [HttpPost]
        [Authorize(Policy = "WriteScope")]       
        public async Task<IActionResult> UpsertContact([FromBody] ContactDto contactDto)
        {
            if (contactDto == null)
            {
                _logger.LogWarning("ContactDto is null in UpsertContact");
                return BadRequest("Contact data is required."); //400
            }
              
            try
            {
                var result = await _contactService.UpsertContactAsync(contactDto);

                if (result?.Status == UpsertStatus.Created)
                {
                    _logger.LogInformation("Contact created successfully with Id={ContactId}", result.ContactDto.Id);
                    return CreatedAtAction(nameof(UpsertContact), new { id = result.ContactDto.Id }, result.ContactDto); // 201

                }
                else // Updated
                {
                    _logger.LogInformation("Contact updated successfully with Id={ContactId}", result?.ContactDto.Id);
                    return Ok(result?.ContactDto); //200
                }
                    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in upserting contact");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred  in {nameof(UpsertContact)}: {ex.Message}");
            }           
            
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetContactById(Guid id)
        {
            _logger.LogInformation("GetContactById called with id={id}", id);

            if (id == Guid.Empty)
            {
                _logger.LogWarning("Invalid contact id provided: {id}", id);
                return BadRequest("Invalid contact id."); 
            }

            try
            {
                var result = await _contactService.GetContactByIdAsync(id);

                if (result == null)
                {
                    _logger.LogInformation("Contact not found for id={id}", id);
                    return NotFound("Contact with id not found."); 
                }

                _logger.LogInformation("Returning contact details for id={id}", id);
                return Ok(result); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching contact by id={id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError,$"An unexpected error occurred in {nameof(GetContactById)}: {ex.Message}");           
            }           
        }

        [HttpGet]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetContactList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("GetContactList called with page={Page} pageSize={PageSize}", page, pageSize);

                if (page <= 0 || pageSize <= 0)
                {
                    _logger.LogWarning("Invalid page or pageSize: page={Page}, pageSize={PageSize}", page, pageSize);
                    return BadRequest("Page and pageSize must be greater than zero.");
                }

                var cachedResult = await _contactService.GetContactListAsync(page, pageSize);

                if (cachedResult.Total == 0)
                {
                    _logger.LogInformation("No contacts found for page {Page}", page);
                }
                // Add pagination headers
                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(new
                {
                    cachedResult.Total,
                    pageSize,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(cachedResult.Total / (double)pageSize)
                }));

                _logger.LogInformation("Returning {Count} contacts", cachedResult.Total);
                return Ok(cachedResult.Items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching contact list for page {Page}", page);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred while fetching contacts in {nameof(GetContactList)}: {ex.Message}.");                
            }

        }

    }
}
