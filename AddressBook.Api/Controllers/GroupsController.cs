using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AddressBook.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly ILogger<GroupsController> _logger;
        public GroupsController(IGroupService groupService, ILogger<GroupsController> logger)
        {
            _groupService = groupService;
            _logger = logger;
        }                     

        [HttpPost]
        [Authorize(Policy = "WriteScope")]
        public async Task<IActionResult> UpsertGroup([FromBody] GroupDto groupDto)
        {
            if (groupDto == null)
            {
                _logger.LogWarning("GroupDto is null in UpsertGroup");
                return BadRequest("Group data is required."); //400
            }             

            try
            {
                var result = await _groupService.UpsertGroupAsync(groupDto);
                if (result?.Status == UpsertStatus.Created)
                {
                    _logger.LogInformation("Group created with Id={GroupId}", result.GroupDto.Id);
                    return CreatedAtAction(nameof(UpsertGroup), new { id = result.GroupDto.Id }, result.GroupDto); // 201
                }                   
                else // Updated
                {
                    _logger.LogInformation("Group updated with Id={GroupId}", result?.GroupDto.Id);
                    return Ok(result?.GroupDto); //200
                }                    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in upserting group");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred in {nameof(UpsertGroup)}: {ex.Message}");
            }                       
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetGroupById(Guid id)
        {
            try
            {
                _logger.LogInformation("GetGroupById called with Id={id}", id);
                if (id == Guid.Empty)
                {
                    _logger.LogWarning("Invalid Group id: {Id}", id);
                    return BadRequest("Invalid Group id"); // 400
                }

               var result = await _groupService.GetGroupByIdAsync(id);

                if (result == null)
                {
                    _logger.LogInformation("Group not found for Id={id}", id);
                    return NotFound("Group with id not found."); // 404
                }
                else
                {
                    _logger.LogInformation("Returning group details for Id={id}", id);
                    return Ok(result); // 200
                }                   
             
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching group by Id={id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred in {nameof(GetGroupById)}: {ex.Message}");
            }
        }


        [HttpGet]
        [Authorize(Policy = "ReadScope")]
        public async Task<IActionResult> GetGroupList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("GetGroupList called with page={Page} pageSize={PageSize}", page, pageSize);

                if (page <= 0 || pageSize <= 0)
                {
                    _logger.LogWarning("Invalid page or pageSize: page={Page}, pageSize={PageSize}", page, pageSize);
                    return BadRequest("Page and PageSize must be greater than zero."); 
                }

                var cachedResult = await _groupService.GetGroupListAsync(page, pageSize);

                if (cachedResult.Total == 0)
                {
                    _logger.LogInformation("No groups found for page={Page} pageSize={PageSize}", page, pageSize);
                }

                Response.Headers.Append("X-Pagination", JsonSerializer.Serialize(new
                {
                    cachedResult.Total,
                    pageSize,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(cachedResult.Total / (double)pageSize)
                }));

                _logger.LogInformation("Returning {Count} groups for page={Page} pageSize={PageSize}", cachedResult.Items.Count(), page, pageSize);
                return Ok(cachedResult.Items); 
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching group list for page={Page} pageSize={PageSize}", page, pageSize);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred in {nameof(GetGroupList)} while fetching groups: {ex.Message}"); // 500
            }
        }

    }
}