using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.Json;

namespace AddressBook.Api.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiScope")]
    [ApiController]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        public ContactsController(IContactService contactService)
        {
            _contactService = contactService;
        }


        [HttpPost]
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
        public async Task<IActionResult> GetContactList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                {
                    return BadRequest("Page and pageSize must be greater than zero.");
                }

                var (contactList, totalCount) = await _contactService.GetContactListAsync(page, pageSize);

                if (totalCount == 0)
                {
                    return NotFound("No contacts found.");
                }

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(new
                {
                    totalCount,
                    pageSize,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }));

                return Ok(contactList); // 200
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred while fetching contacts in {nameof(GetContactList)}: {ex.Message}.");                
            }

        }

    }
}
