using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AddressBook.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;
        public GroupsController(IGroupService groupService)
        {
            _groupService = groupService;
        }                     

        [HttpPost]
        [Authorize(Policy = "WriteScope")]
        public async Task<IActionResult> UpsertGroup([FromBody] GroupDto groupDto)
        {
            if (groupDto == null)
                return BadRequest("Group data is required."); //400

            try
            {
                var result = await _groupService.UpsertGroupAsync(groupDto);
                if (result?.Status == UpsertStatus.Created)
                    return CreatedAtAction(nameof(UpsertGroup), new { id = result.GroupDto.Id }, result.GroupDto); // 201
                else // Updated
                    return Ok(result?.GroupDto); //200
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred in {nameof(UpsertGroup)}: {ex.Message}");
            }                       
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetGroupById(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    return BadRequest("Invalid Group id."); // 400

                var result = await _groupService.GetGroupByIdAsync(id);

                if (result == null)
                    return NotFound("Group with id not found."); // 404

                return Ok(result); // 200
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred in {nameof(GetGroupById)}: {ex.Message}");
            }
        }


        [HttpGet]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetGroupList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page <= 0 || pageSize <= 0)
                {
                    return BadRequest("Page and PageSize must be greater than zero."); // 400
                }

                var (items, totalCount) = await _groupService.GetGroupListAsync(page, pageSize);

                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                var paginationHeader = new
                {
                    totalCount,
                    pageSize,
                    currentPage = page,
                    totalPages
                };

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(paginationHeader));

                return Ok(items); // 200    
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred in {nameof(GetGroupList)} while fetching groups: {ex.Message}"); // 500
            }
        }

    }
}