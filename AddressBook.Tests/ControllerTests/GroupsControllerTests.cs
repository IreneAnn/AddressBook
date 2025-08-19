using AddressBook.Api.Controllers;
using AddressBook.Application;
using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AddressBook.Tests.ControllerTests
{
    public class GroupsControllerTests
    {
        private readonly Mock<IGroupService> _mockGroupService;
        private readonly GroupsController _controller;

        public GroupsControllerTests()
        {
            _mockGroupService = new Mock<IGroupService>();
            _controller = new GroupsController(_mockGroupService.Object);
        }


        #region UpsertGroup

        [Fact]
        public async Task UpsertGroup_ValidGroup_ReturnsCreated() //200
        {
            // Arrange
            var groupDto = new GroupDto { Id = Guid.NewGuid(), Name = "Test Group" };
            _mockGroupService.Setup(s => s.UpsertGroupAsync(groupDto))
                .ReturnsAsync(new UpsertGroupResult { Status = UpsertStatus.Created, GroupDto = groupDto });

            // Act
            var result = await _controller.UpsertGroup(groupDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(groupDto, createdResult.Value);
        }

        [Fact]
        public async Task UpsertGroup_NullGroup_ReturnsBadRequest() //400
        {
            // Act
            var result = await _controller.UpsertGroup(null);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Group data is required.", badRequest.Value);
        }

        [Fact]
        public async Task UpsertGroup_Exception_Returns500() //500
        {
            var groupDto = new GroupDto { Name = "Error Group" };
            _mockGroupService.Setup(s => s.UpsertGroupAsync(groupDto))
                .ThrowsAsync(new Exception("DB error"));

            var result = await _controller.UpsertGroup(groupDto);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        #endregion



        #region GetGroupById

        [Fact]
        public async Task GetGroupById_ValidId_ReturnsOk() //200
        {
            var id = Guid.NewGuid();
            var group = new GroupDto { Id = id, Name = "Group1" };
            _mockGroupService.Setup(s => s.GetGroupByIdAsync(id)).ReturnsAsync(group);

            var result = await _controller.GetGroupById(id);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(group, ok.Value);
        }

        [Fact]
        public async Task GetGroupById_InvalidId_ReturnsBadRequest() //400
        {
            var result = await _controller.GetGroupById(Guid.Empty);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid Group id.", badRequest.Value);
        }

        [Fact]
        public async Task GetGroupById_NotFound_Returns404() //404
        {
            var id = Guid.NewGuid();
            _mockGroupService.Setup(s => s.GetGroupByIdAsync(id)).ReturnsAsync((GroupDto)null);

            var result = await _controller.GetGroupById(id);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Group with id not found.", notFound.Value);
        }

        [Fact]
        public async Task GetGroupById_Exception_Returns500()  //500
        {
            var id = Guid.NewGuid();
            _mockGroupService.Setup(s => s.GetGroupByIdAsync(id)).ThrowsAsync(new Exception("DB error"));

            var result = await _controller.GetGroupById(id);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }


        #endregion


        #region GetGroupList

        [Fact]
        public async Task GetGroupList_Valid_ReturnsOkWithHeader() //200
        {
            var groups = new List<GroupDto>
            {
                new GroupDto { Id = Guid.NewGuid(), Name = "Group1" },
                new GroupDto { Id = Guid.NewGuid(), Name = "Group2" }
            };
            int totalCount = groups.Count;
            int page = 1;
            int pageSize = 10;

            _mockGroupService.Setup(s => s.GetGroupListAsync(page, pageSize))
                .ReturnsAsync((groups, totalCount));

            // Initialize ControllerContext
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Response = { Body = new MemoryStream() }
                }
            };

            var result = await _controller.GetGroupList(page, pageSize);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(groups, ok.Value);
        }

        [Fact]
        public async Task GetGroupList_InvalidPage_ReturnsBadRequest() //400
        {
            var result = await _controller.GetGroupList(0, 0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Page and PageSize must be greater than zero.", badRequest.Value);
        }

        [Fact]
        public async Task GetGroupList_Exception_Returns500()  //500
        {
            _mockGroupService.Setup(s => s.GetGroupListAsync(1, 10)).ThrowsAsync(new Exception("DB error"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    Response = { Body = new MemoryStream() }
                }
            };

            var result = await _controller.GetGroupList(1, 10);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        
        #endregion


        private class PaginationHeader
        {
            public int TotalCount { get; set; }
            public int PageSize { get; set; }
            public int CurrentPage { get; set; }
            public int TotalPages { get; set; }
        }
    }
}
